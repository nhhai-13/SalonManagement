using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

/// <summary>Supplies the reception desk with the live items that need action during a shift.</summary>
[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/operations")]
public sealed class ReceptionOperationsController(ApplicationDbContext db, AppointmentLifecycleService lifecycle, TimeProvider timeProvider) : ControllerBase
{
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
            .Where(item => item.Status == "Completed" && item.Invoice?.Status != "Paid")
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
            .Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (appointment.Status != "Completed") return Conflict(new { error = "Cần thợ xác nhận hoàn tất dịch vụ trước khi thanh toán." });
        var availableServices = await db.Services.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ServiceName)
            .Select(item => new CheckoutServiceOption(item.ServiceId, item.ServiceName, item.Price, item.DurationMinutes)).ToListAsync();
        return Ok(new CheckoutDetailsResponse(
            appointment.AppointmentId,
            appointment.CustomerNameOverride ?? appointment.Customer.FullName,
            appointment.Customer.Phone,
            appointment.Stylist.FullName,
            appointment.AppointmentServices.Select(item => new CheckoutLine(item.ServiceId, item.Service.ServiceName, item.Price, item.DurationMinutes)).ToList(),
            availableServices));
    }

    [HttpPost("appointments/{appointmentId:int}/payment")]
    public async Task<IActionResult> TakePayment(int appointmentId, [FromBody] TakePaymentRequest request)
    {
        var method = request.Method?.Trim();
        if (method is not ("Cash" or "Card" or "Transfer"))
            return BadRequest(new { error = "Vui lòng chọn phương thức thanh toán." });
        if (method == "Transfer" && string.IsNullOrWhiteSpace(request.TransactionReference))
            return BadRequest(new { error = "Vui lòng nhập mã giao dịch hoặc ghi chú đối soát." });

        var appointment = await db.Appointments
            .Include(item => item.AppointmentServices)
            .Include(item => item.Invoice).ThenInclude(item => item!.Payments)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return NotFound(new { error = "Không tìm thấy lịch hẹn." });
        if (appointment.Status != "Completed") return Conflict(new { error = "Chỉ có thể thu tiền cho lịch đã hoàn tất." });
        if (appointment.Invoice?.Status == "Paid") return Conflict(new { error = "Phiếu này đã được thanh toán." });

        if (request.Services is not null)
        {
            var selectedIds = request.Services.Select(item => item.ServiceId).Where(id => id > 0).Distinct().ToList();
            if (selectedIds.Count == 0) return BadRequest(new { error = "Phiếu thanh toán cần có ít nhất một dịch vụ." });
            var services = await db.Services.Where(item => item.IsActive && selectedIds.Contains(item.ServiceId)).ToListAsync();
            if (services.Count != selectedIds.Count) return BadRequest(new { error = "Dịch vụ phát sinh không hợp lệ." });
            db.AppointmentServices.RemoveRange(appointment.AppointmentServices);
            appointment.AppointmentServices = services.Select(service => new AppointmentService
            {
                ServiceId = service.ServiceId,
                Price = service.Price,
                DurationMinutes = service.DurationMinutes
            }).ToList();
        }

        var total = appointment.AppointmentServices.Sum(item => item.Price);
        var invoice = appointment.Invoice ?? new Invoice
        {
            AppointmentId = appointment.AppointmentId,
            Subtotal = total,
            TotalAmount = total,
            DiscountAmount = 0,
            Status = "Unpaid",
            CreatedAt = SalonClock.GetLocalNow(timeProvider)
        };
        if (appointment.Invoice is null) db.Invoices.Add(invoice);
        invoice.Subtotal = total;
        invoice.TotalAmount = Math.Max(0, total - invoice.DiscountAmount);
        invoice.Status = "Paid";
        db.Payments.Add(new Payment
        {
            Invoice = invoice,
            Amount = invoice.TotalAmount,
            PaymentMethod = method,
            PaymentStatus = "Paid",
            PaidAt = SalonClock.GetLocalNow(timeProvider),
            TransactionCode = string.IsNullOrWhiteSpace(request.TransactionReference) ? null : request.TransactionReference.Trim()
        });
        await db.SaveChangesAsync();
        return Ok(new { message = "Đã ghi nhận thanh toán.", invoiceId = invoice.InvoiceId, total = invoice.TotalAmount });
    }

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
        string.Join(", ", appointment.AppointmentServices.Select(item => item.Service.ServiceName)));
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
public sealed record CheckoutQueueItem(int AppointmentId, string CustomerName, string StylistName, decimal TotalAmount, string Services);
public sealed record CheckoutLine(int ServiceId, string ServiceName, decimal Price, int DurationMinutes);
public sealed record CheckoutServiceOption(int ServiceId, string ServiceName, decimal Price, int DurationMinutes);
public sealed record CheckoutDetailsResponse(int AppointmentId, string CustomerName, string Phone, string StylistName, IReadOnlyList<CheckoutLine> Services, IReadOnlyList<CheckoutServiceOption> AvailableServices);
public sealed record CheckoutServiceChange(int ServiceId);
public sealed record TakePaymentRequest(string? Method, string? TransactionReference = null, IReadOnlyList<CheckoutServiceChange>? Services = null);
