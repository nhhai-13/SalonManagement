using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Reception;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[Route("reception/schedule")]
public sealed class ReceptionScheduleController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date)
    {
        var selectedDate = (date ?? DateTime.Today).Date;
        var hours = await db.BusinessHours.AsNoTracking().SingleOrDefaultAsync(item => item.DayOfWeek == selectedDate.DayOfWeek);
        var opensAt = hours?.OpensAt?.ToTimeSpan() ?? TimeSpan.FromHours(8);
        var closesAt = hours?.ClosesAt?.ToTimeSpan() ?? TimeSpan.FromHours(18);
        var shifts = await db.WorkSchedules.AsNoTracking()
            .Where(item => item.WorkDate.Date == selectedDate && item.Status == "Working")
            .Select(item => new { item.StylistId, item.StartTime, item.EndTime })
            .ToListAsync();
        var dayOffStylistIds = await db.StylistDaysOff.AsNoTracking()
            .Where(item => item.OffDate == selectedDate)
            .Select(item => item.StylistId)
            .ToListAsync();
        var displayedStylistIds = shifts.Select(item => item.StylistId).Union(dayOffStylistIds).ToList();
        var stylistNames = await db.Stylists.AsNoTracking()
            .Where(item => item.IsActive && displayedStylistIds.Contains(item.StylistId))
            .OrderBy(item => item.FullName)
            .Select(item => new { item.StylistId, item.FullName })
            .ToListAsync();
        var dayOffSet = dayOffStylistIds.ToHashSet();
        var stylists = stylistNames.Select(stylist => new DailyScheduleStylist(
            stylist.StylistId,
            stylist.FullName,
            shifts.Where(shift => shift.StylistId == stylist.StylistId)
                .OrderBy(shift => shift.StartTime)
                .Select(shift => new DailyScheduleShift(shift.StartTime, shift.EndTime)).ToList(),
            dayOffSet.Contains(stylist.StylistId))).ToList();
        var appointmentRows = await db.Appointments.AsNoTracking()
            .Where(item => item.AppointmentDate.Date == selectedDate && item.Status != "Cancelled" && item.Status != "Rejected" && item.Status != "NoShow")
            .OrderBy(item => item.StartTime)
            .Select(item => new
            {
                item.AppointmentId, item.StylistId, CustomerName = item.Customer.FullName,
                Services = item.AppointmentServices.Select(service => service.Service.ServiceName).ToList(),
                item.StartTime, item.EndTime, item.Status
            })
            .ToListAsync();
        var appointments = appointmentRows
            .Select(item => new DailyScheduleAppointment(item.AppointmentId, item.StylistId, item.CustomerName, string.Join(", ", item.Services), item.StartTime, item.EndTime, item.Status))
            .ToList();
        return View(new DailyScheduleViewModel(selectedDate, opensAt, closesAt, stylists, appointments));
    }
}
