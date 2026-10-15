using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record AppointmentLookupItem(string Reference, DateTime Date, TimeSpan StartTime, TimeSpan EndTime, IReadOnlyList<string> Services, string StylistName, int DurationMinutes, string Status, DateTime? CancelledAt, string? CancelledBy);
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
            var appointment = await DetailQuery().FirstOrDefaultAsync(item => item.BookingReference == value.ToUpperInvariant());
            return appointment is null ? new([], "Không tìm thấy lịch hẹn.") : new([ToItem(appointment)], null);
        }
        var phone = BookingService.NormalizePhone(value);
        if (phone is null) return new([], "Nhập mã 8 ký tự hoặc số điện thoại hợp lệ.");
        // SQLite cannot sort TimeSpan values in SQL.  Read the customer's small
        // set of active appointments first, then order by time in memory.
        var appointments = (await DetailQuery()
            .Where(item => item.Customer.Phone == phone && item.Status != "Cancelled" && item.Status != "Completed" && item.Status != "NoShow")
            .ToListAsync())
            .OrderBy(item => item.AppointmentDate)
            .ThenBy(item => item.StartTime)
            .ToList();
        var items = appointments.Select(ToItem).ToList();
        return items.Count == 0 ? new([], "Không tìm thấy lịch hẹn.") : new(items, null);
    }

    private IQueryable<Appointment> DetailQuery() => db.Appointments.AsNoTracking().Include(item => item.Customer).Include(item => item.Stylist).Include(item => item.AppointmentServices).ThenInclude(item => item.Service);
    private static AppointmentLookupItem ToItem(Appointment appointment)
    {
        var duration = appointment.AppointmentServices.Sum(item => item.DurationMinutes);
        return new(appointment.BookingReference, appointment.AppointmentDate, appointment.StartTime, appointment.EndTime, appointment.AppointmentServices.Select(item => item.Service.ServiceName).ToList(), appointment.Stylist?.FullName ?? "Chưa phân công", duration, StatusLabel(appointment.Status), appointment.CancelledAt, appointment.CancelledBy);
    }
    private static string StatusLabel(string status) => status switch { "Pending" or "PendingConfirmation" => "Chờ xác nhận", "Confirmed" => "Đã xác nhận", "Rejected" => "Đã từ chối", "InProgress" => "Đang thực hiện", "Completed" => "Đã hoàn thành", "Cancelled" => "Đã huỷ", "NoShow" => "Không đến", _ => status };
}
