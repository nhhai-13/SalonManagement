using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Services;

public sealed record BookingRequest(DateTime Date, TimeSpan StartTime, IReadOnlyCollection<int> ServiceIds, string FullName, string Phone, string? Email = null, string? Notes = null);
public sealed record BookingConfirmation(string Reference, string StylistName, IReadOnlyList<string> Services, DateTime Date, TimeSpan StartTime, TimeSpan EndTime);
public sealed record BookingCreationResult(BookingConfirmation? Confirmation, string? ErrorCode, string? Message, IReadOnlyDictionary<string, string>? FieldErrors = null)
{
    public static BookingCreationResult Rejected(string code, string message) => new(null, code, message);
    public static BookingCreationResult Invalid(IReadOnlyDictionary<string, string> errors) => new(null, "invalid_details", "Vui lòng kiểm tra lại thông tin đã nhập.", errors);
}

public sealed class BookingService(ApplicationDbContext db, TimeProvider timeProvider)
{
    private const string ReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly SemaphoreSlim ConfirmationLock = new(1, 1);

    public async Task<BookingCreationResult> CreateAsync(BookingRequest request)
    {
        var errors = new Dictionary<string, string>();
        var name = request.FullName?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 100) errors["fullName"] = "Họ tên phải có từ 1 đến 100 ký tự.";
        var phone = NormalizePhone(request.Phone);
        if (phone is null) errors["phone"] = "Số điện thoại phải gồm đúng 10 chữ số.";
        if (!string.IsNullOrWhiteSpace(request.Email) && !new EmailAddressAttribute().IsValid(request.Email.Trim())) errors["email"] = "Email không đúng định dạng.";
        if ((request.Notes?.Length ?? 0) > 300) errors["notes"] = "Ghi chú không được vượt quá 300 ký tự.";
        if (request.ServiceIds.Count == 0) errors["services"] = "Vui lòng chọn ít nhất một dịch vụ.";
        if (errors.Count > 0) return BookingCreationResult.Invalid(errors);
        if (request.Date.Date < SalonClock.GetLocalNow(timeProvider).Date)
            return BookingCreationResult.Rejected("past_date", "Ngày hẹn không hợp lệ.");
        var now = SalonClock.GetLocalNow(timeProvider);
        if (request.Date.Date == now.Date && request.StartTime < now.TimeOfDay.Add(TimeSpan.FromHours(1)))
            return BookingCreationResult.Rejected("slot_unavailable", "Khung giờ này không còn trống. Vui lòng chọn khung giờ khác.");
        var services = await db.Services.Where(item => item.IsActive && request.ServiceIds.Contains(item.ServiceId)).ToListAsync();
        if (services.Count != request.ServiceIds.Distinct().Count())
            return BookingCreationResult.Rejected("invalid_services", "Dịch vụ đã chọn không còn khả dụng.");
        var endTime = request.StartTime.Add(TimeSpan.FromMinutes(services.Sum(item => item.DurationMinutes)));
        var hours = await db.BusinessHours.SingleOrDefaultAsync(item => item.DayOfWeek == request.Date.DayOfWeek);
        if (hours?.IsClosed == true || (hours?.OpensAt is not null && request.StartTime < hours.OpensAt.Value.ToTimeSpan()) || (hours?.ClosesAt is not null && endTime > hours.ClosesAt.Value.ToTimeSpan()))
            return BookingCreationResult.Rejected("slot_unavailable", "Khung giờ này không còn trống. Vui lòng chọn khung giờ khác.");

        var required = request.ServiceIds.Distinct().ToHashSet();
        var stylists = await db.Stylists.Where(item => item.IsActive)
            .Include(item => item.Services)
            .Include(item => item.WorkSchedules.Where(schedule => schedule.WorkDate == request.Date.Date))
            .ToListAsync();
        var daysOff = await db.StylistDaysOff.Where(item => item.OffDate == request.Date.Date).Select(item => item.StylistId).ToListAsync();
        var breaks = await db.StylistBreaks.Where(item => item.BreakDate == request.Date.Date).ToListAsync();
        var appointments = await db.Appointments.Where(item => item.AppointmentDate == request.Date.Date && item.Status != "Cancelled" && item.Status != "NoShow").ToListAsync();
        var stylist = stylists.FirstOrDefault(candidate => !daysOff.Contains(candidate.StylistId)
            && required.All(serviceId => candidate.Services.Any(skill => skill.ServiceId == serviceId))
            && candidate.WorkSchedules.Any(schedule => schedule.StartTime <= request.StartTime && endTime <= schedule.EndTime)
            && !breaks.Where(item => item.StylistId == candidate.StylistId).Any(item => item.StartTime < endTime && request.StartTime < item.EndTime)
            && !appointments.Where(item => item.StylistId == candidate.StylistId).Any(item => item.StartTime < endTime && request.StartTime < item.EndTime));
        if (stylist is null)
            return BookingCreationResult.Rejected("slot_unavailable", "Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác.");

        await ConfirmationLock.WaitAsync();
        try
        {
            var activeCount = await db.Appointments.Include(item => item.Customer)
                .CountAsync(item => item.Customer.Phone == phone && item.Status != "Cancelled" && item.Status != "Completed" && item.Status != "NoShow");
            if (activeCount >= 3)
                return BookingCreationResult.Rejected("appointment_limit", "Bạn đã có 3 lịch hẹn chưa hoàn tất. Vui lòng huỷ bớt lịch cũ hoặc liên hệ tiệm.");
            var customer = await db.Customers.FirstOrDefaultAsync(item => item.Phone == phone)
                ?? new Customer { FullName = name, Phone = phone!, Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim() };
            if (customer.CustomerId == 0) db.Customers.Add(customer); else { customer.FullName = name; customer.Email = string.IsNullOrWhiteSpace(request.Email) ? customer.Email : request.Email.Trim(); }
            var reference = await CreateReferenceAsync();
            db.Appointments.Add(new Appointment { Customer = customer, StylistId = stylist.StylistId, AppointmentDate = request.Date.Date, StartTime = request.StartTime, EndTime = endTime, Status = "Confirmed", BookingReference = reference, Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), AppointmentServices = services.Select(service => new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes }).ToList() });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { return BookingCreationResult.Rejected("slot_unavailable", "Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác."); }
            return new BookingCreationResult(new BookingConfirmation(reference, stylist.FullName, services.Select(item => item.ServiceName).ToList(), request.Date.Date, request.StartTime, endTime), null, null);
        }
        finally { ConfirmationLock.Release(); }
    }

    private async Task<string> CreateReferenceAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var reference = string.Concat(Enumerable.Range(0, 8).Select(_ => ReferenceAlphabet[Random.Shared.Next(ReferenceAlphabet.Length)]));
            if (!await db.Appointments.AnyAsync(item => item.BookingReference == reference)) return reference;
        }
        throw new InvalidOperationException("Unable to generate a unique booking reference.");
    }

    public static string? NormalizePhone(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84") && digits.Length == 11) digits = "0" + digits[2..];
        return digits.Length == 10 ? digits : null;
    }
}
