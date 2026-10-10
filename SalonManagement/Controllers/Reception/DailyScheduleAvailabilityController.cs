using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Reception;

[ApiController]
[Authorize(Roles = UserRoles.Receptionist)]
[Route("api/reception/daily-schedule")]
public sealed class DailyScheduleAvailabilityController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(DateTime? date)
    {
        var selectedDate = (date ?? DateTime.Today).Date;
        var shifts = await db.WorkSchedules.AsNoTracking()
            .Where(item => item.WorkDate.Date == selectedDate && item.Status == "Working" && item.Stylist.IsActive)
            .OrderBy(item => item.StartTime)
            .Select(item => new { item.StylistId, StylistName = item.Stylist.FullName, item.StartTime, item.EndTime })
            .ToListAsync();
        var daysOff = await db.StylistDaysOff.AsNoTracking()
            .Where(item => item.OffDate == selectedDate && item.Stylist.IsActive)
            .Select(item => item.StylistId)
            .ToListAsync();
        var allIds = shifts.Select(item => item.StylistId).Union(daysOff).ToList();
        var names = await db.Stylists.AsNoTracking().Where(item => allIds.Contains(item.StylistId))
            .ToDictionaryAsync(item => item.StylistId, item => item.FullName);
        var daysOffSet = daysOff.ToHashSet();
        var stylists = allIds.Distinct().OrderBy(id => names[id]).Select(id => new
        {
            stylistId = id, name = names[id], isDayOff = daysOffSet.Contains(id),
            shifts = shifts.Where(shift => shift.StylistId == id).Select(shift => new { startTime = shift.StartTime, endTime = shift.EndTime })
        });
        var appointments = await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => item.AppointmentDate.Date == selectedDate && item.Status != "Cancelled" && item.Status != "Rejected" && item.Status != "NoShow")
            .Select(item => new
            {
                item.AppointmentId, item.StylistId, customerName = item.Customer.FullName,
                services = item.AppointmentServices.Select(service => service.Service.ServiceName), item.StartTime, item.EndTime, item.Status
            })
            .ToListAsync();
        return Ok(new { date = selectedDate, stylists, appointments });
    }
}
