using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;

namespace SalonManagement.Services;

public sealed record AppointmentLookupItem(string Reference, DateTime Date, TimeSpan StartTime);
public sealed record AppointmentLookupResult(IReadOnlyList<AppointmentLookupItem> Items, string? Error);

public sealed class AppointmentLookupService(ApplicationDbContext db)
{
    public async Task<AppointmentLookupResult> SearchAsync(string? input)
    {
        var value = input?.Trim() ?? string.Empty;
        if (value.Length == 0) return new([], "Vui lòng nhập mã lịch hẹn hoặc số điện thoại.");
        if (value.Length == 8)
        {
            if (!value.All(character => char.IsLetterOrDigit(character))) return new([], "Mã lịch hẹn phải gồm đúng 8 ký tự chữ hoặc số.");
            var appointment = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(item => item.BookingReference.ToUpper() == value.ToUpper());
            return appointment is null ? new([], "Không tìm thấy lịch hẹn.") : new([new(appointment.BookingReference, appointment.AppointmentDate, appointment.StartTime)], null);
        }
        var phone = BookingService.NormalizePhone(value);
        if (phone is null) return new([], "Nhập mã 8 ký tự hoặc số điện thoại hợp lệ.");
        var items = await db.Appointments.AsNoTracking().Include(item => item.Customer)
            .Where(item => item.Customer.Phone == phone && item.Status != "Cancelled" && item.Status != "Completed" && item.Status != "NoShow")
            .OrderBy(item => item.AppointmentDate).ThenBy(item => item.StartTime)
            .Select(item => new AppointmentLookupItem(item.BookingReference, item.AppointmentDate, item.StartTime)).ToListAsync();
        return items.Count == 0 ? new([], "Không tìm thấy lịch hẹn.") : new(items, null);
    }
}
