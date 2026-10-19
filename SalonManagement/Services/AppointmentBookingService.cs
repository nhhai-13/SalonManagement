using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Data;
using SalonManagement.Models;
using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Services;

public sealed record BookingRequest(DateTime Date, TimeSpan StartTime, IReadOnlyCollection<int> ServiceIds, string FullName, string? Phone, string? Email = null, string? Notes = null, int? StylistId = null);
public sealed record BookingConfirmation(string Reference, string StylistName, IReadOnlyList<string> Services, DateTime Date, TimeSpan StartTime, TimeSpan EndTime, int NoShowCount);
public sealed record BookingCreationResult(BookingConfirmation? Confirmation, string? ErrorCode, string? Message, IReadOnlyDictionary<string, string>? FieldErrors = null)
{
    public static BookingCreationResult Rejected(string code, string message) => new(null, code, message);
    public static BookingCreationResult Invalid(IReadOnlyDictionary<string, string> errors) => new(null, "invalid_details", "Vui lòng kiểm tra lại thông tin đã nhập.", errors);
}

public sealed class AppointmentBookingService
{
    private readonly ApplicationDbContext db;
    private readonly TimeProvider timeProvider;
    private readonly StylistInAppNotificationService notifications;

    [ActivatorUtilitiesConstructor]
    public AppointmentBookingService(ApplicationDbContext db, TimeProvider timeProvider, StylistInAppNotificationService notifications)
    {
        this.db = db;
        this.timeProvider = timeProvider;
        this.notifications = notifications;
    }

    // Keeps existing isolated tests and maintenance tools source-compatible.
    public AppointmentBookingService(ApplicationDbContext db, TimeProvider timeProvider)
        : this(db, timeProvider, new StylistInAppNotificationService(db, timeProvider)) { }

    private const string ReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly SemaphoreSlim ConfirmationLock = new(1, 1);

    public async Task<BookingCreationResult> CreateAsync(BookingRequest request)
    {
        var errors = new Dictionary<string, string>();
        var name = request.FullName?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 100) errors["fullName"] = "Họ tên phải có từ 1 đến 100 ký tự.";
        var enteredPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        var phone = BookingService.NormalizePhone(enteredPhone);
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (enteredPhone is not null && phone is null) errors["phone"] = "Số điện thoại phải gồm đúng 10 chữ số.";
        if (email is not null && !new EmailAddressAttribute().IsValid(email)) errors["email"] = "Email không đúng định dạng.";
        if (phone is null && email is null) errors["contact"] = "Vui lòng nhập số điện thoại hoặc email.";
        if ((request.Notes?.Length ?? 0) > 300) errors["notes"] = "Ghi chú không được vượt quá 300 ký tự.";
        if (request.ServiceIds.Count == 0) errors["services"] = "Vui lòng chọn ít nhất một dịch vụ.";
        if (errors.Count > 0) return BookingCreationResult.Invalid(errors);
        if (request.Date.Date < SalonClock.GetLocalNow(timeProvider).Date) return BookingCreationResult.Rejected("past_date", "Ngày hẹn không hợp lệ.");

        var services = await db.Services.Where(item => item.IsActive && request.ServiceIds.Contains(item.ServiceId)).ToListAsync();
        if (services.Count != request.ServiceIds.Distinct().Count()) return BookingCreationResult.Rejected("invalid_services", "Dịch vụ đã chọn không còn khả dụng.");
        var endTime = request.StartTime.Add(TimeSpan.FromMinutes(services.Sum(item => item.DurationMinutes)));
        var stylists = await db.Stylists.Where(item => item.IsActive).Include(item => item.Services).Include(item => item.WorkSchedules.Where(schedule => schedule.WorkDate == request.Date.Date)).ToListAsync();
        var required = request.ServiceIds.Distinct().ToHashSet();
        var appointments = await db.Appointments.Where(item => item.AppointmentDate == request.Date.Date && item.Status != "Cancelled" && item.Status != "NoShow" && item.Status != "Rejected").ToListAsync();
        var selectedStylistId = request.StylistId is > 0 ? request.StylistId : null;
        var stylist = stylists.FirstOrDefault(candidate =>
            (!selectedStylistId.HasValue || candidate.StylistId == selectedStylistId.Value) &&
            required.All(serviceId => candidate.Services.Any(skill => skill.ServiceId == serviceId)) &&
            candidate.WorkSchedules.Any(schedule => schedule.StartTime <= request.StartTime && endTime <= schedule.EndTime) &&
            !appointments.Where(item => item.StylistId == candidate.StylistId)
                .Any(item => item.StartTime < endTime && request.StartTime < item.EndTime));
        if (stylist is null) return BookingCreationResult.Rejected("slot_unavailable", "Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác.");

        await ConfirmationLock.WaitAsync();
        try
        {
            // The availability query above is only a preview. Re-check after taking the
            // booking lock so a competing request cannot commit the same interval.
            // SQLite cannot translate TimeSpan comparisons.  Scope the query to
            // this stylist and day in SQL, then safely check the small list of
            // appointments for overlapping time ranges in memory.
            var dayAppointments = await db.Appointments.AsNoTracking()
                .Where(item =>
                    item.StylistId == stylist.StylistId &&
                    item.AppointmentDate == request.Date.Date &&
                    item.Status != "Cancelled" &&
                    item.Status != "NoShow" &&
                    item.Status != "Rejected" &&
                    item.Status != "Completed")
                .Select(item => new { item.StartTime, item.EndTime })
                .ToListAsync();
            var collisionExists = dayAppointments.Any(item =>
                item.StartTime < endTime && request.StartTime < item.EndTime);
            if (collisionExists)
                return BookingCreationResult.Rejected("slot_unavailable", "Khung giờ này vừa có người đặt. Vui lòng chọn khung giờ khác.");

            var profiles = new CustomerProfileService(db);
            var existingCustomer = phone is not null
                ? await profiles.FindByPhoneAsync(phone)
                : await profiles.FindByEmailAsync(email!);
            var activeCount = existingCustomer is null ? 0 : await db.Appointments.CountAsync(item =>
                item.CustomerId == existingCustomer.CustomerId && item.Status != "Cancelled" && item.Status != "Completed" && item.Status != "NoShow" && item.Status != "Rejected");
            if (activeCount >= 3) return BookingCreationResult.Rejected("appointment_limit", "Bạn đã có 3 lịch hẹn chưa hoàn tất. Vui lòng huỷ bớt lịch cũ hoặc liên hệ tiệm.");
            var noShowCount = existingCustomer is null ? 0 : await db.Appointments.CountAsync(item => item.CustomerId == existingCustomer.CustomerId && item.Status == "NoShow");
            var hasNameConflict = existingCustomer is not null && !CustomerProfileService.NamesEquivalent(existingCustomer.FullName, name);
            if (existingCustomer is not null && email is not null && string.IsNullOrWhiteSpace(existingCustomer.Email))
                existingCustomer.Email = email;
            var customer = existingCustomer ?? (phone is not null
                ? await profiles.FindOrCreateAsync(name, phone, email, updateExistingName: false, overwriteExistingEmail: false)
                : await profiles.CreateEmailOnlyAsync(name, email!));
            var reference = await CreateReferenceAsync();
            // Bookings submitted from the public site must be reviewed by reception.
            // Staff-created appointments continue to use their existing workflow/statuses.
            var appointment = new Appointment { Customer = customer, StylistId = stylist.StylistId, AppointmentDate = request.Date.Date, StartTime = request.StartTime, EndTime = endTime, Status = "PendingConfirmation", BookingReference = reference, BookingSource = "Online", CustomerNameOverride = hasNameConflict ? name : null, NameConflictResolution = hasNameConflict ? "PendingReceptionReview" : null, Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), AppointmentServices = services.Select(service => new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes }).ToList() };
            db.Appointments.Add(appointment);
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return BookingCreationResult.Rejected("slot_unavailable", "Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác."); }
            notifications.Add(stylist.Email, appointment.AppointmentId, "AppointmentAssigned", $"Bạn được gán lịch hẹn mới #{reference} lúc {request.StartTime:hh\\:mm} ngày {request.Date:dd/MM}.");
            await db.SaveChangesAsync();
            return new(new(reference, stylist.FullName, services.Select(item => item.ServiceName).ToList(), request.Date.Date, request.StartTime, endTime, noShowCount), null, null);
        }
        finally { ConfirmationLock.Release(); }
    }

    private async Task<string> CreateReferenceAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++) { var reference = string.Concat(Enumerable.Range(0, 8).Select(_ => ReferenceAlphabet[Random.Shared.Next(ReferenceAlphabet.Length)])); if (!await db.Appointments.AnyAsync(item => item.BookingReference == reference)) return reference; }
        throw new InvalidOperationException("Unable to generate a unique booking reference.");
    }
}
