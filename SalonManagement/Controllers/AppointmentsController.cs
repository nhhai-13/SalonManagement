using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[Authorize(Roles = RoleGroups.InternalStaff)]
public class AppointmentsController(ApplicationDbContext db, ILogger<AppointmentsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date)
    {
        var selectedDate = (date ?? VietnamTime.Today).Date;
        ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
        var appointments = await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Stylist)
            .Include(item => item.AppointmentServices)
                .ThenInclude(item => item.Service)
            .Include(item => item.Invoice)
            .Where(item => item.AppointmentDate.Date == selectedDate)
            .OrderBy(item => item.StartTime)
            .ToListAsync();

        return View(appointments);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Stylist)
            .Include(item => item.AppointmentServices)
                .ThenInclude(item => item.Service)
            .Include(item => item.AppointmentServices)
                .ThenInclude(item => item.Stylist)
            .Include(item => item.Invoice)
            .FirstOrDefaultAsync(item => item.AppointmentId == id);

        if (appointment is null)
        {
            return NotFound();
        }

        var history = new List<AppointmentHistoryRow>();
        var historyLoadFailed = false;
        var isNewCustomer = false;
        try
        {
            var previousAppointments = await db.Appointments.AsNoTracking()
                .Where(item => item.CustomerId == appointment.CustomerId
                    && item.AppointmentId != appointment.AppointmentId
                    && (item.Status == "Completed" || item.Status == "Closed"))
                .OrderByDescending(item => item.AppointmentDate)
                .ThenByDescending(item => item.StartTime)
                .Take(10)
                .Select(item => new
                {
                    item.AppointmentDate,
                    Services = item.AppointmentServices.Select(service => new
                    {
                        service.Service.ServiceName,
                        StylistName = service.Stylist != null
                            ? service.Stylist.FullName
                            : item.Stylist.FullName,
                        service.TechnicalNotes
                    }).ToList()
                })
                .ToListAsync();

            isNewCustomer = previousAppointments.Count == 0;
            history = previousAppointments
                .SelectMany(item => item.Services.DefaultIfEmpty().Select(service =>
                    new AppointmentHistoryRow
                    {
                        AppointmentDate = item.AppointmentDate,
                        ServiceName = service?.ServiceName ?? "Chưa có dịch vụ",
                        StylistName = service?.StylistName ?? "Chưa phân công",
                        TechnicalNotes = service?.TechnicalNotes
                    }))
                .ToList();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load customer visit history for appointment {AppointmentId}.", id);
            historyLoadFailed = true;
        }

        return View(new AppointmentDetailsViewModel
        {
            Appointment = appointment,
            History = history,
            IsNewCustomer = isNewCustomer,
            HistoryLoadFailed = historyLoadFailed
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.Stylist)]
    public async Task<IActionResult> SaveTechnicalNote(int id, string? technicalNotes)
    {
        if (technicalNotes?.Length > 500)
        {
            TempData["Error"] = "Ghi chú kỹ thuật không được vượt quá 500 ký tự.";
            return RedirectToAction(nameof(Details), new { id = await GetAppointmentId(id) });
        }

        var item = await db.AppointmentServices
            .Include(service => service.Appointment)
            .Include(service => service.Stylist)
            .FirstOrDefaultAsync(service => service.AppointmentServiceId == id);
        if (item is null)
        {
            return NotFound();
        }

        var assignedStylist = item.Stylist
            ?? await db.Stylists.FindAsync(item.Appointment.StylistId);
        if (assignedStylist is null || !string.Equals(
                assignedStylist.Email,
                User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        item.TechnicalNotes = string.IsNullOrWhiteSpace(technicalNotes)
            ? null
            : technicalNotes;
        item.TechnicalNotesUpdatedBy = User.Identity?.Name;
        item.TechnicalNotesUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã lưu ghi chú kỹ thuật.";
        return RedirectToAction(nameof(Details), new { id = item.AppointmentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = UserRoles.Stylist)]
    public async Task<IActionResult> CompleteService(int id)
    {
        var item = await db.AppointmentServices
            .Include(service => service.Appointment)
            .Include(service => service.Stylist)
            .FirstOrDefaultAsync(service => service.AppointmentServiceId == id);
        if (item is null)
        {
            return NotFound();
        }

        var assignedStylist = item.Stylist
            ?? await db.Stylists.FindAsync(item.Appointment.StylistId);
        if (assignedStylist is null || !string.Equals(
                assignedStylist.Email,
                User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name,
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        item.IsCompleted = true;
        item.CompletedAt ??= DateTime.UtcNow;
        var allServices = await db.AppointmentServices
            .Where(service => service.AppointmentId == item.AppointmentId)
            .ToListAsync();
        if (allServices.All(service => service.IsCompleted))
        {
            item.Appointment.Status = "Completed";
            item.Appointment.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Đã đánh dấu dịch vụ hoàn tất.";
        return RedirectToAction(nameof(Details), new { id = item.AppointmentId });
    }

    private async Task<int> GetAppointmentId(int appointmentServiceId) =>
        await db.AppointmentServices.Where(item => item.AppointmentServiceId == appointmentServiceId)
            .Select(item => item.AppointmentId).FirstOrDefaultAsync();
}
