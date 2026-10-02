using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record BookingRequest(DateTime Date, TimeSpan StartTime, IReadOnlyCollection<int> ServiceIds, string FullName, string Phone);
public sealed record BookingConfirmation(string Reference, string StylistName, IReadOnlyList<string> Services, DateTime Date, TimeSpan StartTime, TimeSpan EndTime);
public sealed record BookingCreationResult(BookingConfirmation? Confirmation, string? ErrorCode, string? Message)
{
    public static BookingCreationResult Rejected(string code, string message) => new(null, code, message);
}

public sealed class BookingService(ApplicationDbContext db, TimeProvider timeProvider)
{
    private const string ReferenceAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<BookingCreationResult> CreateAsync(BookingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone) || request.ServiceIds.Count == 0)
            return BookingCreationResult.Rejected("invalid_details", "Vui lòng nhập họ tên, số điện thoại và dịch vụ.");
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

        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Phone == request.Phone.Trim())
            ?? new Customer { FullName = request.FullName.Trim(), Phone = request.Phone.Trim() };
        if (customer.CustomerId == 0) db.Customers.Add(customer); else customer.FullName = request.FullName.Trim();
        var reference = await CreateReferenceAsync();
        var appointment = new Appointment
        {
            Customer = customer,
            StylistId = stylist.StylistId,
            AppointmentDate = request.Date.Date,
            StartTime = request.StartTime,
            EndTime = endTime,
            Status = "Confirmed",
            BookingReference = reference,
            AppointmentServices = services.Select(service => new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes }).ToList()
        };
        db.Appointments.Add(appointment);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return BookingCreationResult.Rejected("slot_unavailable", "Khung giờ này vừa được đặt. Vui lòng chọn khung giờ khác."); }
        return new BookingCreationResult(new BookingConfirmation(reference, stylist.FullName, services.Select(item => item.ServiceName).ToList(), request.Date.Date, request.StartTime, endTime), null, null);
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
}
