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
        var stylists = await db.WorkSchedules.AsNoTracking().Where(item => item.WorkDate.Date == selectedDate && item.Status == "Working" && item.Stylist.IsActive).OrderBy(item => item.Stylist.FullName).Select(item => new DailyScheduleStylist(item.StylistId, item.Stylist.FullName, item.StartTime, item.EndTime)).ToListAsync();
        var appointments = (await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => item.AppointmentDate.Date == selectedDate && item.Status != "Cancelled" && item.Status != "Rejected" && item.Status != "NoShow")
            .ToListAsync())
            .Select(item => new DailyScheduleAppointment(item.AppointmentId, item.StylistId, item.Customer.FullName, string.Join(", ", item.AppointmentServices.Select(service => service.Service.ServiceName)), item.StartTime, item.EndTime, item.Status))
            .ToList();
        return View(new DailyScheduleViewModel(selectedDate, stylists, appointments));
    }
}
