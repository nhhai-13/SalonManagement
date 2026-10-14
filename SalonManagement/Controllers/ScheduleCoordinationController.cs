using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

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
        var appointmentEntities = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.StylistId == stylistId &&
                appointment.AppointmentDate >= dayStart &&
                appointment.AppointmentDate < nextDay &&
                (appointment.Status == "Pending" || appointment.Status == "Confirmed" || appointment.Status == "Arrived"))
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.AppointmentServices)
                .ThenInclude(item => item.Service)
                    .ThenInclude(service => service.ServiceGroup)
            .OrderBy(appointment => appointment.StartTime)
            .ThenBy(appointment => appointment.AppointmentId)
            .ToListAsync();

        var activeStylists = await dbContext.Stylists
            .AsNoTracking()
            .Where(stylist => stylist.IsActive && stylist.StylistId != stylistId)
            .Include(stylist => stylist.WorkSchedules)
            .ToListAsync();

        var dayAppointments = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.AppointmentDate >= dayStart &&
                appointment.AppointmentDate < nextDay)
            .ToListAsync();

        var storedBusinessHours = await dbContext.BusinessHours
            .AsNoTracking()
            .SingleOrDefaultAsync(hours => hours.DayOfWeek == date.Value.DayOfWeek);
        var businessHours = new BusinessHourResponse(
            date.Value.DayOfWeek,
            storedBusinessHours?.IsClosed ?? false,
            storedBusinessHours?.OpensAt ?? new TimeOnly(8, 0),
            storedBusinessHours?.ClosesAt ?? new TimeOnly(17, 0),
            storedBusinessHours?.TimeZoneId ?? BusinessHour.SalonTimeZone);

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
                ScheduleCoordinationPlanner.GetDuration(appointment),
                appointment.Status,
                ScheduleCoordinationPlanner.BuildPlan(
                    appointment,
                    stylistId.Value,
                    date.Value,
                    activeStylists,
                    dayAppointments,
                    businessHours))).ToArray();

        return Ok(new ScheduleCoordinationAppointmentsResponse(
            stylistId.Value,
            date.Value,
            appointments.Length,
            appointments));
    }

    [HttpPost("transfers/preview")]
    public async Task<ActionResult<ScheduleTransferBatchResponse>> PreviewTransfers(
        [FromBody] ScheduleTransferBatchRequest request,
        CancellationToken cancellationToken)
    {
        var response = await new ScheduleTransferProcessor(dbContext)
            .PreviewAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("transfers")]
    public async Task<ActionResult<ScheduleTransferBatchResponse>> TransferAppointments(
        [FromBody] ScheduleTransferBatchRequest request,
        CancellationToken cancellationToken)
    {
        var response = await new ScheduleTransferProcessor(dbContext)
            .TransferAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("follow-ups/sync")]
    public async Task<ActionResult<ScheduleFollowUpSyncResponse>> SyncFollowUps(
        [FromBody] ScheduleFollowUpSyncRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StylistId <= 0)
        {
            return BadRequest(new { message = "Vui lòng chọn thợ cần điều phối." });
        }

        var dayStart = request.Date.ToDateTime(TimeOnly.MinValue);
        var nextDay = dayStart.AddDays(1);
        var sources = await dbContext.Appointments
            .Where(item => item.StylistId == request.StylistId &&
                item.AppointmentDate >= dayStart && item.AppointmentDate < nextDay &&
                (item.Status == "Pending" || item.Status == "Confirmed" || item.Status == "Arrived"))
            .Include(item => item.Customer)
            .Include(item => item.AppointmentServices)
                .ThenInclude(service => service.Service)
                    .ThenInclude(service => service.ServiceGroup)
            .ToListAsync(cancellationToken);
        var stylists = await dbContext.Stylists
            .AsNoTracking()
            .Where(item => item.IsActive && item.StylistId != request.StylistId)
            .Include(item => item.WorkSchedules)
            .ToListAsync(cancellationToken);
        var dayAppointments = await dbContext.Appointments.AsNoTracking()
            .Where(item => item.AppointmentDate >= dayStart && item.AppointmentDate < nextDay)
            .ToListAsync(cancellationToken);
        var storedHours = await dbContext.BusinessHours.AsNoTracking()
            .SingleOrDefaultAsync(item => item.DayOfWeek == request.Date.DayOfWeek, cancellationToken);
        var businessHours = new BusinessHourResponse(
            request.Date.DayOfWeek,
            storedHours?.IsClosed ?? false,
            storedHours?.OpensAt ?? new TimeOnly(8, 0),
            storedHours?.ClosesAt ?? new TimeOnly(17, 0),
            storedHours?.TimeZoneId ?? BusinessHour.SalonTimeZone);

        var addedOrUpdated = 0;
        foreach (var appointment in sources)
        {
            var plan = ScheduleCoordinationPlanner.BuildPlan(
                appointment, request.StylistId, request.Date, stylists, dayAppointments, businessHours);
            if (plan.HasOptions)
            {
                var staleNoOption = await dbContext.ScheduleContactFollowUps
                    .SingleOrDefaultAsync(item => item.AppointmentId == appointment.AppointmentId &&
                        item.Category == ScheduleContactCategories.NoAlternative, cancellationToken);
                if (staleNoOption is not null)
                {
                    dbContext.ScheduleContactFollowUps.Remove(staleNoOption);
                }
                continue;
            }

            var followUp = await dbContext.ScheduleContactFollowUps
                .SingleOrDefaultAsync(item => item.AppointmentId == appointment.AppointmentId, cancellationToken);
            if (followUp?.Category == ScheduleContactCategories.TransferFailed)
            {
                continue;
            }

            if (followUp is null)
            {
                followUp = new ScheduleContactFollowUp
                {
                    AppointmentId = appointment.AppointmentId,
                    OriginalAppointmentDate = appointment.AppointmentDate,
                    OriginalStartTime = appointment.StartTime,
                    CreatedAt = DateTime.Now
                };
                dbContext.ScheduleContactFollowUps.Add(followUp);
            }

            followUp.Category = ScheduleContactCategories.NoAlternative;
            followUp.Reason = BuildNoAlternativeReason(plan);
            followUp.UpdatedAt = DateTime.Now;
            addedOrUpdated++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ScheduleFollowUpSyncResponse(addedOrUpdated, addedOrUpdated));
    }

    [HttpGet("follow-ups")]
    public async Task<ActionResult<ScheduleFollowUpListResponse>> GetFollowUps(
        [FromQuery] DateOnly? date,
        [FromQuery] string? contactStatus,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        IQueryable<ScheduleContactFollowUp> query = dbContext.ScheduleContactFollowUps.AsNoTracking()
            .Include(item => item.Appointment).ThenInclude(item => item.Customer)
            .Include(item => item.Appointment).ThenInclude(item => item.AppointmentServices)
                .ThenInclude(item => item.Service);
        query = query.Where(item => item.Appointment.Status == "Pending" ||
            item.Appointment.Status == "Confirmed" || item.Appointment.Status == "Arrived");
        if (date.HasValue)
        {
            var dayStart = date.Value.ToDateTime(TimeOnly.MinValue);
            var nextDay = dayStart.AddDays(1);
            query = query.Where(item => item.OriginalAppointmentDate >= dayStart &&
                item.OriginalAppointmentDate < nextDay);
        }

        if (!string.IsNullOrWhiteSpace(contactStatus))
        {
            query = query.Where(item => item.ContactStatus == contactStatus);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category);
        }

        var entities = await query
            .OrderBy(item => item.OriginalAppointmentDate)
            .ThenBy(item => item.OriginalStartTime)
            .ThenBy(item => item.AppointmentId)
            .ToListAsync(cancellationToken);
        var items = entities.Select(item => new ScheduleFollowUpItem(
            item.ScheduleContactFollowUpId,
            item.AppointmentId,
            item.Category,
            item.Category == ScheduleContactCategories.NoAlternative
                ? "Chưa tìm được phương án"
                : "Chuyển lịch thất bại",
            item.Appointment.Customer.FullName,
            item.Appointment.Customer.Phone,
            item.Appointment.AppointmentServices
                .OrderBy(service => service.AppointmentServiceId)
                .Select(service => service.Service.ServiceName)
                .ToArray(),
            DateOnly.FromDateTime(item.OriginalAppointmentDate),
            item.OriginalStartTime.ToString(@"hh\:mm"),
            item.Reason,
            item.ContactStatus,
            item.ContactNote,
            item.ContactedAt)).ToArray();
        return Ok(new ScheduleFollowUpListResponse(items.Length, items));
    }

    [HttpPut("follow-ups/{id:int}/contact")]
    public async Task<IActionResult> UpdateFollowUpContact(
        int id,
        [FromBody] ScheduleFollowUpUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContactStatus is not (ScheduleContactStatuses.Pending or ScheduleContactStatuses.Contacted))
        {
            return BadRequest(new { message = "Trạng thái liên hệ không hợp lệ." });
        }

        var followUp = await dbContext.ScheduleContactFollowUps
            .SingleOrDefaultAsync(item => item.ScheduleContactFollowUpId == id, cancellationToken);
        if (followUp is null)
        {
            return NotFound(new { message = "Không tìm thấy lịch cần liên hệ." });
        }

        followUp.ContactStatus = request.ContactStatus;
        followUp.ContactNote = string.IsNullOrWhiteSpace(request.ContactNote)
            ? null
            : request.ContactNote.Trim();
        followUp.ContactedAt = request.ContactStatus == ScheduleContactStatuses.Contacted
            ? DateTime.Now
            : null;
        followUp.UpdatedAt = DateTime.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Đã cập nhật kết quả liên hệ." });
    }

    [HttpGet("notifications/{id:int}")]
    public async Task<ActionResult<ScheduleChangeNotificationStatus>> GetScheduleChangeNotification(
        int id,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.ScheduleChangeNotifications.AsNoTracking()
            .Where(notification => notification.ScheduleChangeNotificationId == id)
            .Select(notification => new ScheduleChangeNotificationStatus(
                notification.ScheduleChangeNotificationId,
                notification.AppointmentId,
                notification.Version,
                notification.Status,
                notification.AttemptCount,
                notification.TransferredAt,
                notification.SentAt,
                notification.NextAttemptAt,
                notification.LastError))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? NotFound(new { message = "Không tìm thấy email thông báo lịch hẹn." })
            : Ok(item);
    }

    private static string BuildNoAlternativeReason(ScheduleCoordinationPlan plan)
    {
        var reasons = plan.Stylists
            .SelectMany(item => item.ReasonsUnavailableAtOriginalTime)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return reasons.Length == 0
            ? "Không tìm thấy thợ thay thế hoặc khung giờ phù hợp."
            : string.Join(" ", reasons);
    }
}
