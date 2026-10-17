using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace SalonManagement.Controllers.Reception;

/// <summary>Supplies the reception desk with the live items that need action during a shift.</summary>
[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/operations")]
public sealed class ReceptionOperationsController : ControllerBase
{
    private readonly ApplicationDbContext db;
    private readonly AppointmentLifecycleService lifecycle;
    private readonly TimeProvider timeProvider;
    private readonly IEmailService emailService;
    private readonly ILogger<ReceptionOperationsController> logger;

    [ActivatorUtilitiesConstructor]
    public ReceptionOperationsController(
        ApplicationDbContext db,
        AppointmentLifecycleService lifecycle,
        TimeProvider timeProvider,
        IEmailService emailService,
        ILogger<ReceptionOperationsController> logger)
    {
        this.db = db;
        this.lifecycle = lifecycle;
        this.timeProvider = timeProvider;
        this.emailService = emailService;
        this.logger = logger;
    }

    // Convenience constructor for legacy isolated tests. The app always injects
    // the concrete email service and logger through dependency injection.
    public ReceptionOperationsController(ApplicationDbContext db, AppointmentLifecycleService lifecycle, TimeProvider timeProvider)
        : this(db, lifecycle, timeProvider, new NoOpEmailService(), NullLogger<ReceptionOperationsController>.Instance) { }

    [HttpGet]
    public async Task<ActionResult<ReceptionOperationsSnapshot>> Get()
    {
        var now = SalonClock.GetLocalNow(timeProvider);
        var today = now.Date;
        var appointments = await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Stylist)
            .Include(item => item.Invoice)
            .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => item.AppointmentDate == today)
            .ToListAsync();

        var activeStylists = await db.Stylists.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => item.StylistId)
            .ToListAsync();
        var activeStylistIds = activeStylists.ToHashSet();
        var workingSchedules = await db.WorkSchedules.AsNoTracking()
            .Where(item => item.WorkDate == today && item.Status == "Working")
            .ToListAsync();
        var onShiftIds = workingSchedules
            .Where(item => item.StartTime <= now.TimeOfDay && now.TimeOfDay < item.EndTime)
            .Select(item => item.StylistId)
            .Where(activeStylistIds.Contains)
            .ToHashSet();
        if (workingSchedules.Count == 0) onShiftIds = activeStylistIds;

        var busyStylistIds = appointments
            .Where(item => item.Status is "Arrived" or "InProgress")
            .Select(item => item.StylistId)
            .ToHashSet();
        var upcomingLimit = now.AddMinutes(60);
        var upcoming = appointments
            .Where(item => item.Status == "Confirmed")
            .Where(item => item.AppointmentDate.Date.Add(item.StartTime) >= now && item.AppointmentDate.Date.Add(item.StartTime) <= upcomingLimit)
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.AppointmentId)
            .Select(ToUpcoming)
            .ToList();
        var checkout = appointments
            .Where(item => item.Status is "Arrived" or "InProgress" or "Completed")
            .Where(item => item.Invoice?.Status != "Paid")
            .OrderBy(item => item.ActualCompletedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .Select(ToCheckout)
            .ToList();

        return Ok(new ReceptionOperationsSnapshot(
            now,
            appointments.Count(item => item.Status is not ("Cancelled" or "Rejected")),
            appointments.Count(item => item.Status is "Arrived" or "InProgress"),
            checkout.Count,
            onShiftIds.Count(id => !busyStylistIds.Contains(id)),
            onShiftIds.Count,
            upcoming,
            checkout));
    }

    [HttpPost("appointments/{appointmentId:int}/check-in")]
    public async Task<IActionResult> CheckIn(int appointmentId)
    {
        var result = await lifecycle.CheckInAsync(appointmentId);
        return result.Succeeded ? Ok(new { message = "Đã check-in khách." }) : BadRequest(new { error = result.Error });
    }

    [HttpGet("appointments/{appointmentId:int}/checkout")]
    public async Task<ActionResult<CheckoutDetailsResponse>> CheckoutDetails(int appointmentId)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Stylist)
            .Include(item => item.Invoice)
            .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (!CanManageInvoice(appointment.Status)) return Conflict(new { error = "Cần check-in khách trước khi mở hóa đơn." });
        var availableServices = await db.Services.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ServiceName)
            .Select(item => new CheckoutServiceOption(item.ServiceId, item.ServiceName, item.Price, item.DurationMinutes)).ToListAsync();
        return Ok(new CheckoutDetailsResponse(
            appointment.AppointmentId,
            appointment.CustomerNameOverride ?? appointment.Customer.FullName,
            appointment.Customer.Phone,
            appointment.Stylist.FullName,
            appointment.AppointmentServices.Select(item => new CheckoutLine(item.ServiceId, item.Service.ServiceName, item.Price, item.DurationMinutes)).ToList(),
            availableServices,
            appointment.Invoice?.Subtotal ?? appointment.AppointmentServices.Sum(item => item.Price),
            appointment.Invoice?.DiscountAmount ?? 0,
            appointment.Invoice?.PromotionCode));
    }

    [HttpPut("appointments/{appointmentId:int}/invoice")]
    public async Task<IActionResult> SaveInvoice(int appointmentId, [FromBody] SaveInvoiceRequest request)
    {
        var appointment = await db.Appointments
            .Include(item => item.AppointmentServices)
            .Include(item => item.Invoice)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (!CanManageInvoice(appointment.Status)) return Conflict(new { error = "Cần check-in khách trước khi cập nhật hóa đơn." });
        if (appointment.Invoice?.Status == "Paid") return Conflict(new { error = "Phiếu này đã được thanh toán." });

        var result = await UpdateInvoiceAsync(appointment, request.Services, request.PromotionCode);
        if (result.Error is not null) return BadRequest(new { error = result.Error });
        await db.SaveChangesAsync();
        return Ok(new { message = "Đã lưu hóa đơn nháp.", subtotal = result.Invoice!.Subtotal, discountAmount = result.Invoice.DiscountAmount, total = result.Invoice.TotalAmount, promotionCode = result.Invoice.PromotionCode });
    }

    [HttpPost("appointments/{appointmentId:int}/payment")]
    public async Task<IActionResult> TakePayment(int appointmentId, [FromBody] TakePaymentRequest request)
    {
        var method = request.Method?.Trim();
        if (method is not ("Cash" or "Card" or "Transfer"))
            return BadRequest(new { error = "Vui lòng chọn phương thức thanh toán." });

        var appointment = await db.Appointments
            .Include(item => item.Customer)
            .Include(item => item.Stylist)
            .Include(item => item.AppointmentServices)
            .Include(item => item.Invoice).ThenInclude(item => item!.Payments)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (!CanManageInvoice(appointment.Status)) return Conflict(new { error = "Cần check-in khách trước khi thanh toán." });
        if (appointment.Invoice?.Status == "Paid") return Conflict(new { error = "Phiếu này đã được thanh toán." });

        var result = await UpdateInvoiceAsync(appointment, request.Services, request.PromotionCode);
        if (result.Error is not null) return BadRequest(new { error = result.Error });
        var invoice = result.Invoice!;
        invoice.Status = "Paid";
        var transactionCode = string.IsNullOrWhiteSpace(request.TransactionReference)
            ? BuildTransferReference(appointment.AppointmentId)
            : request.TransactionReference.Trim();
        db.Payments.Add(new Payment
        {
            Invoice = invoice,
            Amount = invoice.TotalAmount,
            PaymentMethod = method,
            PaymentStatus = "Paid",
            PaidAt = SalonClock.GetLocalNow(timeProvider),
            TransactionCode = method == "Transfer" ? transactionCode : null
        });
        var reviewToken = await CreateReviewInvitationAsync(appointment);
        await db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(reviewToken) && !string.IsNullOrWhiteSpace(appointment.Customer.Email))
        {
            var reviewLink = $"{Request.Scheme}://{Request.Host}/reviews/link/{reviewToken}";
            try
            {
                await emailService.SendServiceReviewEmailAsync(
                    appointment.Customer.Email,
                    appointment.CustomerNameOverride ?? appointment.Customer.FullName,
                    reviewLink,
                    SalonClock.GetLocalNow(timeProvider).AddDays(7));
                var invitation = await db.ServiceReviews.SingleAsync(item => item.AppointmentId == appointment.AppointmentId);
                invitation.EmailSentAt = SalonClock.GetLocalNow(timeProvider);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Không thể gửi email mời đánh giá cho lịch hẹn {AppointmentId}", appointment.AppointmentId);
            }
        }

        return Ok(new { message = "Đã ghi nhận thanh toán và tạo lời mời đánh giá dịch vụ.", invoiceId = invoice.InvoiceId, subtotal = invoice.Subtotal, discountAmount = invoice.DiscountAmount, total = invoice.TotalAmount, promotionCode = invoice.PromotionCode });
    }

    private async Task<(Invoice? Invoice, string? Error)> UpdateInvoiceAsync(Appointment appointment, IReadOnlyList<CheckoutServiceChange>? requestedServices, string? promotionCode)
    {
        List<Service>? selectedServices = null;
        if (requestedServices is not null)
        {
            var selectedIds = requestedServices.Select(item => item.ServiceId).Where(id => id > 0).Distinct().ToList();
            if (selectedIds.Count == 0) return (null, "Phiếu thanh toán cần có ít nhất một dịch vụ.");
            selectedServices = await db.Services.Where(item => item.IsActive && selectedIds.Contains(item.ServiceId)).ToListAsync();
            if (selectedServices.Count != selectedIds.Count) return (null, "Dịch vụ phát sinh không hợp lệ.");
        }

        var subtotal = (selectedServices is not null ? selectedServices.Select(item => item.Price) : appointment.AppointmentServices.Select(item => item.Price)).Sum();
        var promotion = await ResolvePromotionAsync(promotionCode, subtotal);
        if (promotion.Error is not null) return (null, promotion.Error);
        if (selectedServices is not null)
        {
            db.AppointmentServices.RemoveRange(appointment.AppointmentServices);
            appointment.AppointmentServices = selectedServices.Select(service => new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes }).ToList();
        }

        var invoice = appointment.Invoice ?? new Invoice { AppointmentId = appointment.AppointmentId, CreatedAt = SalonClock.GetLocalNow(timeProvider), Status = "Unpaid" };
        if (appointment.Invoice is null) db.Invoices.Add(invoice);
        invoice.Subtotal = subtotal;
        invoice.DiscountAmount = promotion.DiscountAmount;
        invoice.TotalAmount = subtotal - promotion.DiscountAmount;
        invoice.PromotionCode = promotion.Promotion?.Code;
        return (invoice, null);
    }

    [HttpPost("appointments/{appointmentId:int}/promotion")]
    public async Task<IActionResult> PreviewPromotion(int appointmentId, [FromBody] PromotionPreviewRequest request)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (appointment.Status != "Completed") return Conflict(new { error = "Chỉ có thể áp mã cho lịch đã hoàn tất." });

        decimal subtotal;
        if (request.Services is null)
        {
            subtotal = appointment.AppointmentServices.Sum(item => item.Price);
        }
        else
        {
            var ids = request.Services.Select(item => item.ServiceId).Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return BadRequest(new { error = "Phiếu thanh toán cần có ít nhất một dịch vụ." });
            var services = await db.Services.AsNoTracking().Where(item => item.IsActive && ids.Contains(item.ServiceId)).ToListAsync();
            if (services.Count != ids.Count) return BadRequest(new { error = "Dịch vụ phát sinh không hợp lệ." });
            subtotal = services.Sum(item => item.Price);
        }

        var promotion = await ResolvePromotionAsync(request.PromotionCode, subtotal);
        if (promotion.Error is not null) return BadRequest(new { error = promotion.Error, subtotal });
        return Ok(new
        {
            message = promotion.Promotion is null ? "Đã bỏ mã giảm giá." : $"Áp dụng thành công: {promotion.Promotion.Name}.",
            code = promotion.Promotion?.Code,
            promotionName = promotion.Promotion?.Name,
            subtotal,
            discountAmount = promotion.DiscountAmount,
            total = subtotal - promotion.DiscountAmount,
            discountType = promotion.Promotion?.DiscountType,
            discountValue = promotion.Promotion?.DiscountValue
        });
    }

    private async Task<(PromotionCode? Promotion, decimal DiscountAmount, string? Error)> ResolvePromotionAsync(string? requestedCode, decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(requestedCode)) return (null, 0, null);
        var code = requestedCode.Trim().ToUpperInvariant();
        var promotion = await db.PromotionCodes.AsNoTracking().SingleOrDefaultAsync(item => item.Code == code);
        if (promotion is null) return (null, 0, "Mã giảm giá không tồn tại.");
        if (!promotion.IsActive) return (null, 0, "Mã giảm giá này đã ngừng áp dụng.");

        var now = SalonClock.GetLocalNow(timeProvider);
        if (now < promotion.StartsAt) return (null, 0, $"Mã giảm giá có hiệu lực từ {promotion.StartsAt:dd/MM/yyyy}.");
        if (now > promotion.EndsAt) return (null, 0, $"Mã giảm giá đã hết hạn từ {promotion.EndsAt:dd/MM/yyyy}.");

        var discount = promotion.DiscountType == "Percentage"
            ? Math.Round(subtotal * promotion.DiscountValue / 100m, 0, MidpointRounding.AwayFromZero)
            : promotion.DiscountValue;
        return (promotion, Math.Clamp(discount, 0, subtotal), null);
    }

    private static bool CanManageInvoice(string status) => status is "Arrived" or "InProgress" or "Completed";

    private async Task<string?> CreateReviewInvitationAsync(Appointment appointment)
    {
        if (await db.ServiceReviews.AnyAsync(item => item.AppointmentId == appointment.AppointmentId)) return null;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        db.ServiceReviews.Add(new ServiceReview
        {
            AppointmentId = appointment.AppointmentId,
            TokenHash = HashReviewToken(token),
            CreatedAt = SalonClock.GetLocalNow(timeProvider),
            ExpiresAt = SalonClock.GetLocalNow(timeProvider).AddDays(7)
        });
        return token;
    }

    private static string HashReviewToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string BuildTransferReference(int appointmentId) =>
        $"FT{SalonClock.GetLocalNow(timeProvider):yyMMddHHmmss}{appointmentId:D5}";

    private static UpcomingCheckInItem ToUpcoming(Appointment appointment) => new(
        appointment.AppointmentId,
        appointment.CustomerNameOverride ?? appointment.Customer.FullName,
        appointment.Customer.Phone,
        appointment.Stylist.FullName,
        string.Join(", ", appointment.AppointmentServices.Select(item => item.Service.ServiceName)),
        appointment.AppointmentDate,
        appointment.StartTime,
        appointment.IsLate,
        appointment.LateMinutes);

    private static CheckoutQueueItem ToCheckout(Appointment appointment) => new(
        appointment.AppointmentId,
        appointment.CustomerNameOverride ?? appointment.Customer.FullName,
        appointment.Stylist.FullName,
        appointment.Invoice?.TotalAmount > 0 ? appointment.Invoice.TotalAmount : appointment.AppointmentServices.Sum(item => item.Price),
        string.Join(", ", appointment.AppointmentServices.Select(item => item.Service.ServiceName)),
        appointment.Status);

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword) => Task.CompletedTask;
        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink) => Task.CompletedTask;
        public Task SendPasswordResetCodeAsync(string toEmail, string resetCode) => Task.CompletedTask;
        public Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode) => Task.CompletedTask;
        public Task SendAppointmentChangeEmailAsync(string toEmail, string stylistName, DateTime appointmentDate, TimeSpan startTime) => Task.CompletedTask;
        public Task SendServiceReviewEmailAsync(string toEmail, string customerName, string reviewLink, DateTime expiresAt) => Task.CompletedTask;
        public Task SendAppointmentNotificationEmailAsync(string toEmail, string customerName, string reference, string services, string stylistName, DateTime appointmentDate, TimeSpan startTime, bool isReminder) => Task.CompletedTask;
    }
}

public sealed record ReceptionOperationsSnapshot(
    DateTime GeneratedAt,
    int TotalAppointments,
    int CheckedInAppointments,
    int PendingCheckoutCount,
    int IdleStylistCount,
    int OnShiftStylistCount,
    IReadOnlyList<UpcomingCheckInItem> UpcomingCheckIns,
    IReadOnlyList<CheckoutQueueItem> PendingCheckouts);

public sealed record UpcomingCheckInItem(int AppointmentId, string CustomerName, string Phone, string StylistName, string Services, DateTime AppointmentDate, TimeSpan StartTime, bool IsLate, int? LateMinutes);
public sealed record CheckoutQueueItem(int AppointmentId, string CustomerName, string StylistName, decimal TotalAmount, string Services, string Status);
public sealed record CheckoutLine(int ServiceId, string ServiceName, decimal Price, int DurationMinutes);
public sealed record CheckoutServiceOption(int ServiceId, string ServiceName, decimal Price, int DurationMinutes);
public sealed record CheckoutDetailsResponse(int AppointmentId, string CustomerName, string Phone, string StylistName, IReadOnlyList<CheckoutLine> Services, IReadOnlyList<CheckoutServiceOption> AvailableServices, decimal Subtotal, decimal DiscountAmount, string? PromotionCode);
public sealed record CheckoutServiceChange(int ServiceId);
public sealed record TakePaymentRequest(string? Method, string? TransactionReference = null, IReadOnlyList<CheckoutServiceChange>? Services = null, string? PromotionCode = null);
public sealed record SaveInvoiceRequest(IReadOnlyList<CheckoutServiceChange>? Services = null, string? PromotionCode = null);
public sealed record PromotionPreviewRequest(string? PromotionCode, IReadOnlyList<CheckoutServiceChange>? Services = null);
