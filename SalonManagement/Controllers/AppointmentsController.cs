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
    public async Task<IActionResult> Index(DateTime? date, string? status)
    {
        var selectedDate = (date ?? VietnamTime.Today).Date;
        ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
        ViewBag.SelectedStatus = status;
        IQueryable<Appointment> query = db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Stylist)
            .Include(item => item.AppointmentServices)
                .ThenInclude(item => item.Service)
            .Include(item => item.Invoice)
            .Where(item => item.AppointmentDate.Date == selectedDate);
        if (status == "Pending") query = query.Where(item => item.Status == "Pending" || item.Status == "PendingConfirmation");
        else if (status == "Cancelled") query = query.Where(item => item.Status == "Cancelled" || item.Status == "Canceled");
<<<<<<< HEAD
        else if (status is "Confirmed" or "Arrived" or "Completed" or "NoShow") query = query.Where(item => item.Status == status);
        var appointments = await query.ToListAsync();
        appointments = appointments.OrderBy(item => item.StartTime).ToList();
=======
        else if (status == "Completed") query = query.Where(item => item.Status == "Completed" || item.Status == "Closed");
        else if (status is "Confirmed" or "Arrived" or "NoShow") query = query.Where(item => item.Status == status);
        var appointments = await query.OrderBy(item => item.StartTime).ToListAsync();
>>>>>>> origin/develop

        return View(appointments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        var appointment = await db.Appointments
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.AppointmentId == id);

        if (appointment == null) return NotFound();

        if (appointment.Status == "Pending")
        {
            appointment.Status = "Confirmed";
            appointment.ConfirmedAt = DateTime.UtcNow;

            // 1. Đưa email xác nhận vào Hàng đợi (Queue)
            var delivery = new AppointmentEmailDelivery
            {
                AppointmentId = appointment.AppointmentId,
                Kind = AppointmentEmailKinds.Confirmation,
                RecipientEmail = appointment.Customer.Email,
                Status = AppointmentEmailStatuses.Queued,
                AppointmentStartAt = new DateTimeOffset(appointment.AppointmentDate.Add(appointment.StartTime), TimeSpan.FromHours(7)),
                NextAttemptAt = DateTimeOffset.UtcNow, // Gửi ngay lập tức
                CreatedAt = DateTimeOffset.UtcNow,
                AttemptCount = 0
            };
            db.AppointmentEmailDeliveries.Add(delivery);

            // (Tùy chọn) Nếu bạn muốn cập nhật luôn trạng thái Sent/NoEmail trên bảng Appointment để hiển thị lên UI
            if (string.IsNullOrWhiteSpace(appointment.Customer.Email))
            {
                appointment.ConfirmationEmailStatus = "NoEmail";
            }
            else
            {
                appointment.ConfirmationEmailStatus = "Sent"; // Sẽ gửi trong 30s tới
                appointment.ConfirmationEmailSentAt = DateTime.UtcNow;
            }
            db.AppointmentEmailDeliveries.Add(delivery);
            await db.SaveChangesAsync();
            TempData["Success"] = "Đã xác nhận lịch hẹn và đưa email vào hàng đợi.";
        }

        // ==========================================
        // CODE CHO LÁT 2: TẠO EMAIL NHẮC LỊCH (REMINDER)
        // ==========================================
        var startDateTimeOffset = new DateTimeOffset(
            appointment.AppointmentDate.Date.Add(appointment.StartTime),
            TimeSpan.FromHours(7)); // Múi giờ VN

        var reminderDelivery = new AppointmentEmailDelivery
        {
            AppointmentId = appointment.AppointmentId,
            Kind = AppointmentEmailKinds.Reminder, // Đánh dấu là email nhắc
            RecipientEmail = appointment.Customer.Email,
            Status = AppointmentEmailStatuses.Queued, // Đưa vào hàng đợi
            AppointmentStartAt = startDateTimeOffset,
            NextAttemptAt = startDateTimeOffset.AddHours(-24), // Sẽ được Worker tự tính lại
            CreatedAt = DateTimeOffset.UtcNow,
            AttemptCount = 0
        };
        db.AppointmentEmailDeliveries.Add(reminderDelivery);
        // ==========================================

        await db.SaveChangesAsync();


        return RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate.ToString("yyyy-MM-dd") });
    }
    [HttpGet]
    [Authorize(Roles = RoleGroups.FrontDesk)]
    public async Task<IActionResult> NeedToCall()
    {
        var list = await db.Appointments
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.Service)
            .Where(a =>
                // Khách không có email
                (a.Customer.Email == null || a.Customer.Email == "")
                // Lịch chưa bị hủy hoặc hoàn thành
                && a.Status != "Cancelled"
                && a.Status != "Completed"
                // Chưa được đánh dấu là đã gọi
                && (a.Notes == null || !a.Notes.Contains("Đã gọi xác nhận")))
            .ToListAsync();

        // Sắp xếp trên bộ nhớ để tránh lỗi SQLite
        list = list.OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).ToList();

        return View(list);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleGroups.FrontDesk)]
    public async Task<IActionResult> MarkCalled(int id)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment != null)
        {
            // Đánh dấu trạng thái email thành "Đã gọi" để nó biến mất khỏi danh sách
            appointment.ConfirmationEmailStatus = "Called";
            appointment.Notes = (appointment.Notes ?? "") + " | Lễ tân đã gọi xác nhận.";

            await db.SaveChangesAsync();
            TempData["Success"] = "Đã đánh dấu gọi xác nhận thành công.";
        }
        return RedirectToAction(nameof(NeedToCall));
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
