using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[ApiController]
[Route("api/schedule-coordination")]
[Authorize(Roles = RoleGroups.FrontDesk)]
public sealed class ScheduleCoordinationController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet("stylists")]
    public async Task<ActionResult<IReadOnlyList<ScheduleCoordinationStylist>>> GetStylists()
    {
        var stylists = await dbContext.Stylists
            .AsNoTracking()
            .Where(stylist => stylist.IsActive)
            .OrderBy(stylist => stylist.FullName)
            .Select(stylist => new ScheduleCoordinationStylist(stylist.StylistId, stylist.FullName))
            .ToListAsync();

        return Ok(stylists);
    }

    [HttpGet("appointments")]
    public async Task<ActionResult<ScheduleCoordinationAppointmentsResponse>> GetAppointments(
        [FromQuery] int? stylistId,
        [FromQuery] DateOnly? date)
    {
        if (stylistId is null or <= 0 || date is null)
        {
            return BadRequest(new { message = "Vui lòng chọn thợ và ngày nghỉ." });
        }

        var stylistExists = await dbContext.Stylists
            .AsNoTracking()
            .AnyAsync(stylist => stylist.StylistId == stylistId && stylist.IsActive);

        if (!stylistExists)
        {
            return NotFound(new { message = "Không tìm thấy thợ đang hoạt động." });
        }

        var dayStart = date.Value.ToDateTime(TimeOnly.MinValue);
        var nextDay = dayStart.AddDays(1);
        var reviewStatuses = ScheduleCoordinationStatuses.NeedsReview;

        var appointmentEntities = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.StylistId == stylistId &&
                appointment.AppointmentDate >= dayStart &&
                appointment.AppointmentDate < nextDay &&
                reviewStatuses.Contains(appointment.Status))
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.AppointmentServices)
                .ThenInclude(item => item.Service)
            .OrderBy(appointment => appointment.StartTime)
            .ThenBy(appointment => appointment.AppointmentId)
            .ToListAsync();

        var appointments = appointmentEntities.Select(appointment => new ScheduleCoordinationAppointment(
                appointment.AppointmentId,
                appointment.Customer.FullName,
                appointment.Customer.Phone,
                appointment.Customer.Email,
                appointment.AppointmentServices
                    .OrderBy(item => item.AppointmentServiceId)
                    .Select(item => item.Service.ServiceName)
                    .ToArray(),
                appointment.StartTime.ToString(@"hh\:mm"),
                Math.Max(0, (int)(appointment.EndTime - appointment.StartTime).TotalMinutes),
                appointment.Status)).ToArray();

        return Ok(new ScheduleCoordinationAppointmentsResponse(
            stylistId.Value,
            date.Value,
            appointments.Count,
            appointments));
    }
}
