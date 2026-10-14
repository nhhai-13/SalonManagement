using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[Authorize(Roles = RoleGroups.FrontDesk)]
public class InvoicesController(ApplicationDbContext db, IEmailService emailService, ILogger<InvoicesController> logger) : Controller
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
            .Include(item => item.Invoice)
            .Where(item => item.AppointmentDate.Date == selectedDate)
            .OrderBy(item => item.StartTime)
            .ToListAsync();

        return View(appointments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromAppointment(int appointmentId)
    {
        var appointment = await db.Appointments
            .Include(item => item.AppointmentServices)
                .ThenInclude(item => item.Service)
            .Include(item => item.AppointmentServices)
                .ThenInclude(item => item.Stylist)
            .Include(item => item.Invoice)
            .FirstOrDefaultAsync(item => item.AppointmentId == appointmentId);

        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Invoice is not null)
        {
            return RedirectToAction(nameof(Details), new { id = appointment.Invoice.InvoiceId });
        }

        if (appointment.Status != "Completed" || appointment.AppointmentServices.Count == 0 || appointment.AppointmentServices.Any(item => !item.IsCompleted))
        {
            TempData["Error"] = "Chỉ lập phiếu cho lịch hẹn đã hoàn tất dịch vụ.";
            return RedirectToAction(nameof(Index), new { date = appointment.AppointmentDate.ToString("yyyy-MM-dd") });
        }

        var lines = appointment.AppointmentServices.Select(item => new InvoiceLine
        {
            AppointmentServiceId = item.AppointmentServiceId,
            ServiceName = item.Service.ServiceName,
            UnitPrice = item.Price
        }).ToList();
        var total = lines.Sum(line => line.UnitPrice);
        var invoice = new Invoice
        {
            AppointmentId = appointment.AppointmentId,
            Subtotal = total,
            TotalAmount = total,
            DiscountAmount = 0,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Lines = lines
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã lập phiếu từ giá và dịch vụ lưu tại thời điểm đặt lịch.";
        return RedirectToAction(nameof(Details), new { id = invoice.InvoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await LoadInvoice(id);
        if (invoice is null)
        {
            return NotFound();
        }

        var blockers = GetCloseBlockers(invoice);

        return View(new InvoiceDetailsViewModel
        {
            Invoice = invoice,
            AvailableServices = await db.Services.AsNoTracking()
                .Where(item => item.IsActive).OrderBy(item => item.ServiceName).ToListAsync(),
            CanClose = invoice.ClosedAt is null && blockers.Count == 0,
            CloseBlockers = blockers,
            AdjustedTotal = invoice.TotalAmount + invoice.Adjustments.Sum(item => item.AmountDelta)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyDiscount(ApplyDiscountViewModel model)
    {
        var invoice = await LoadInvoice(model.InvoiceId);
        if (invoice is null) return NotFound();
        if (invoice.Status != "Unpaid" || invoice.ClosedAt is not null)
            return LockedInvoice(model.InvoiceId);

        var code = model.Code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            invoice.DiscountCouponId = null;
            invoice.DiscountCoupon = null;
            invoice.DiscountCodeSnapshot = null;
            invoice.DiscountTypeSnapshot = null;
            invoice.DiscountValueSnapshot = null;
            invoice.DiscountAmount = 0;
            Recalculate(invoice);
            await db.SaveChangesAsync();
            TempData["Success"] = "Đã gỡ mã giảm giá.";
            return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
        }

        var coupon = await db.DiscountCoupons.FirstOrDefaultAsync(item => item.Code == code);
        var today = VietnamTime.Today.Date;
        if (coupon is null || today < coupon.StartsOn.Date || today > coupon.EndsOn.Date)
        {
            TempData["Error"] = "Mã giảm giá không tồn tại hoặc đã hết hạn.";
            return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
        }

        invoice.DiscountCouponId = coupon.DiscountCouponId;
        invoice.DiscountCoupon = coupon;
        invoice.DiscountCodeSnapshot = coupon.Code;
        invoice.DiscountTypeSnapshot = coupon.DiscountType;
        invoice.DiscountValueSnapshot = coupon.Value;
        invoice.DiscountAmount = DiscountCalculator.Calculate(invoice.Subtotal, coupon.DiscountType, coupon.Value);
        Recalculate(invoice);
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã áp dụng mã giảm giá.";
        return RedirectToAction(nameof(Details), new { id = model.InvoiceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddService(int id, AddInvoiceServiceViewModel model)
    {
        var invoice = await LoadInvoice(id);
        if (invoice is null) return NotFound();
        if (invoice.Status != "Unpaid" || invoice.ClosedAt is not null)
            return LockedInvoice(id);

        var service = await db.Services.FirstOrDefaultAsync(item => item.ServiceId == model.ServiceId && item.IsActive);
        if (service is null)
        {
            TempData["Error"] = "Dịch vụ không tồn tại hoặc đã ngừng bán.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var appointment = invoice.Appointment;
        var appointmentService = new AppointmentService
        {
            AppointmentId = invoice.AppointmentId,
            ServiceId = service.ServiceId,
            StylistId = appointment.StylistId,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow
        };
        invoice.Lines.Add(new InvoiceLine
        {
            AppointmentService = appointmentService,
            ServiceName = service.ServiceName,
            UnitPrice = service.Price
        });
        Recalculate(invoice);
        await db.SaveChangesAsync();
        if (Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Json(new
            {
                lineId = invoice.Lines.Last().InvoiceLineId,
                serviceName = service.ServiceName,
                stylistName = appointment.Stylist.FullName,
                unitPrice = service.Price,
                formattedUnitPrice = Money(service.Price),
                formattedCollectedAmount = Money(appointmentService.CollectedAmount),
                formattedSubtotal = Money(invoice.Subtotal),
                formattedDiscount = Money(invoice.DiscountAmount),
                formattedTotal = Money(invoice.TotalAmount)
            });
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveService(int id, int lineId)
    {
        var invoice = await LoadInvoice(id);
        if (invoice is null) return NotFound();
        if (invoice.Status != "Unpaid" || invoice.ClosedAt is not null)
            return LockedInvoice(id);

        var line = invoice.Lines.FirstOrDefault(item => item.InvoiceLineId == lineId);
        if (line is null) return NotFound();
        db.InvoiceLines.Remove(line);
        db.AppointmentServices.Remove(line.AppointmentService);
        invoice.Lines.Remove(line);
        Recalculate(invoice);
        await db.SaveChangesAsync();
        if (Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return Json(new { formattedSubtotal = Money(invoice.Subtotal), formattedDiscount = Money(invoice.DiscountAmount), formattedTotal = Money(invoice.TotalAmount) });
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, PaymentViewModel model)
    {
        var invoice = await LoadInvoice(id);
        if (invoice is null) return NotFound();
        if (invoice.Status != "Unpaid" || invoice.ClosedAt is not null)
            return LockedInvoice(id);

        var method = model.PaymentMethod?.Trim();
        var reconciliationNote = model.ReconciliationNote?.Trim();
        if (method is not ("Cash" or "BankTransfer"))
            ModelState.AddModelError(nameof(model.PaymentMethod), "Hãy chọn tiền mặt hoặc chuyển khoản.");
        if (method == "BankTransfer" && string.IsNullOrWhiteSpace(reconciliationNote))
            ModelState.AddModelError(nameof(model.ReconciliationNote), "Chuyển khoản cần ghi chú đối soát.");
        if (invoice.TotalAmount <= 0)
            ModelState.AddModelError(string.Empty, "Phiếu không có dịch vụ hoặc tổng tiền phải lớn hơn 0.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }

        invoice.Payments.Add(new Payment
        {
            Amount = invoice.TotalAmount,
            PaymentMethod = method!,
            PaymentStatus = "Paid",
            TransactionCode = method == "BankTransfer" ? reconciliationNote : null,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
        invoice.Status = "Paid";
        var lines = invoice.Lines.ToList();
        var serviceTotal = lines.Sum(line => line.UnitPrice);
        var remaining = invoice.TotalAmount;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var allocated = index == lines.Count - 1
                ? remaining
                : Math.Round(invoice.TotalAmount * line.UnitPrice / serviceTotal, 2, MidpointRounding.AwayFromZero);
            line.AppointmentService.CollectedAmount = allocated;
            remaining -= allocated;
        }
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã ghi nhận thanh toán.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var invoice = await LoadInvoice(id);
        if (invoice is null) return NotFound();
        var blockers = GetCloseBlockers(invoice);
        if (invoice.ClosedAt is not null)
            return RedirectToAction(nameof(Details), new { id });
        if (blockers.Count > 0)
        {
            TempData["Error"] = string.Join(" ", blockers);
            return RedirectToAction(nameof(Details), new { id });
        }

        var now = DateTime.UtcNow;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        invoice.ClosedAt = now;
        invoice.ClosedByUserId = userId;
        invoice.ClosedByName = User.Identity?.Name;
        invoice.Appointment.ClosedAt = now;
        invoice.Appointment.ClosedByUserId = userId;
        invoice.Appointment.ClosedByName = User.Identity?.Name;
        invoice.Appointment.Status = "Closed";
        invoice.Appointment.UpdatedAt = now;
        await db.SaveChangesAsync();
        if (!await db.StylistReviews.AnyAsync(review => review.AppointmentId == invoice.AppointmentId))
        {
            var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            var review = new StylistReview
            {
                AppointmentId = invoice.AppointmentId, CustomerId = invoice.Appointment.CustomerId,
                StylistId = invoice.Appointment.StylistId,
                ReviewTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
                RequestedAt = now, ExpiresAt = now.AddDays(7)
            };
            db.StylistReviews.Add(review);
            await db.SaveChangesAsync();
            if (!string.IsNullOrWhiteSpace(invoice.Appointment.Customer.Email))
            {
                var reviewUrl = Url.Action("Create", "Reviews", new { token }, Request.Scheme);
                if (!string.IsNullOrWhiteSpace(reviewUrl))
                {
                    try
                    {
                        await emailService.SendReviewRequestEmailAsync(invoice.Appointment.Customer.Email,
                            invoice.Appointment.Customer.FullName, invoice.Appointment.Stylist.FullName, reviewUrl);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Unable to send review request for appointment {AppointmentId}.", invoice.AppointmentId);
                    }
                }
            }
        }
        TempData["Success"] = "Đã đóng lịch hẹn và khóa phiếu.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdjustment(int id, InvoiceAdjustmentViewModel model)
    {
        var invoice = await LoadInvoice(id);
        if (invoice is null) return NotFound();
        if (invoice.ClosedAt is null)
        {
            TempData["Error"] = "Chỉ tạo phiếu điều chỉnh cho phiếu đã đóng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        model.Description = model.Description?.Trim() ?? string.Empty;
        model.Reason = model.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model.Description))
            ModelState.AddModelError(nameof(model.Description), "Hãy nhập nội dung điều chỉnh.");
        if (string.IsNullOrWhiteSpace(model.Reason))
            ModelState.AddModelError(nameof(model.Reason), "Lý do điều chỉnh là bắt buộc.");
        if (model.AmountDelta == 0)
            ModelState.AddModelError(nameof(model.AmountDelta), "Số chênh lệch phải khác 0.");
        var adjustedTotal = invoice.TotalAmount + invoice.Adjustments.Sum(item => item.AmountDelta) + model.AmountDelta;
        if (adjustedTotal < 0)
            ModelState.AddModelError(nameof(model.AmountDelta), "Tổng sau điều chỉnh không thể âm.");

        var targetLine = invoice.Lines.FirstOrDefault(line => line.InvoiceLineId == model.InvoiceLineId);
        if (targetLine is null)
        {
            ModelState.AddModelError(nameof(model.InvoiceLineId), "Chọn một dòng dịch vụ trên phiếu.");
        }
        else if (targetLine.AppointmentService.CollectedAmount + model.AmountDelta < 0)
        {
            ModelState.AddModelError(nameof(model.AmountDelta), "Chênh lệch khiến số tiền gắn với dịch vụ âm.");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage));
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userName))
            return Forbid();

        db.InvoiceAdjustments.Add(new InvoiceAdjustment
        {
            InvoiceId = id,
            InvoiceLineId = targetLine!.InvoiceLineId,
            StylistId = targetLine.AppointmentService.StylistId ?? invoice.Appointment.StylistId,
            Description = model.Description,
            AmountDelta = model.AmountDelta,
            Reason = model.Reason,
            CreatedByUserId = userId,
            CreatedByName = userName,
            CreatedAt = DateTime.UtcNow
        });
        targetLine.AppointmentService.CollectedAmount += model.AmountDelta;
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã ghi nhận phiếu điều chỉnh; phiếu gốc được giữ nguyên.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private IActionResult LockedInvoice(int id)
    {
        TempData["Error"] = "Phiếu đã thanh toán hoặc đã đóng nên không thể sửa.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<Invoice?> LoadInvoice(int id) => await db.Invoices
        .Include(item => item.Appointment)
            .ThenInclude(item => item.Customer)
        .Include(item => item.Appointment)
            .ThenInclude(item => item.AppointmentServices)
        .Include(item => item.Appointment)
            .ThenInclude(item => item.Stylist)
        .Include(item => item.Lines)
            .ThenInclude(item => item.AppointmentService)
                .ThenInclude(item => item.Service)
        .Include(item => item.Lines)
            .ThenInclude(item => item.AppointmentService)
                .ThenInclude(item => item.Stylist)
        .Include(item => item.Lines)
            .ThenInclude(item => item.AppointmentService)
                .ThenInclude(item => item.Appointment)
                    .ThenInclude(item => item.Stylist)
        .Include(item => item.Payments)
        .Include(item => item.DiscountCoupon)
        .Include(item => item.Adjustments)
            .ThenInclude(item => item.Stylist)
        .Include(item => item.Adjustments)
            .ThenInclude(item => item.InvoiceLine)
        .FirstOrDefaultAsync(item => item.InvoiceId == id);

    private static void Recalculate(Invoice invoice)
    {
        invoice.Subtotal = invoice.Lines.Sum(line => line.UnitPrice);
        if (invoice.DiscountCouponId.HasValue && invoice.DiscountTypeSnapshot is not null && invoice.DiscountValueSnapshot.HasValue)
            invoice.DiscountAmount = DiscountCalculator.Calculate(invoice.Subtotal, invoice.DiscountTypeSnapshot, invoice.DiscountValueSnapshot.Value);
        invoice.TotalAmount = invoice.Subtotal - invoice.DiscountAmount;
    }

    private static string Money(decimal amount) =>
        string.Concat(amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")), " đ");

    private static List<string> GetCloseBlockers(Invoice invoice)
    {
        var blockers = new List<string>();
        var services = invoice.Appointment.AppointmentServices;
        if (services.Count == 0 || services.Any(item => !item.IsCompleted))
            blockers.Add("Thợ chưa hoàn tất tất cả dịch vụ.");
        if (invoice.Status != "Paid")
            blockers.Add("Phiếu chưa được thanh toán.");
        return blockers;
    }
}
