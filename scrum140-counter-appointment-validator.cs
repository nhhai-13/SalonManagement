// SCRUM-140 reusable validation service. Adapt entity/DbContext names if needed.
using Microsoft.EntityFrameworkCore;

public static class CounterAppointmentValidator
{
    public static async Task<string?> ValidateAsync(
        ApplicationDbContext db, int stylistId, IReadOnlyCollection<int> serviceIds,
        DateTime date, TimeSpan startTime, TimeSpan endTime)
    {
        var stylist = await db.Stylists.Include(x => x.Services)
            .SingleOrDefaultAsync(x => x.StylistId == stylistId && x.IsActive);
        if (stylist is null) return "Thợ không hợp lệ.";

        var services = await db.Services.Where(x => x.IsActive && serviceIds.Contains(x.ServiceId)).ToListAsync();
        var missing = services.Where(x => !stylist.Services.Any(s => s.ServiceId == x.ServiceId))
            .Select(x => x.ServiceName).ToList();
        if (missing.Count > 0) return "Thợ không thực hiện được: " + string.Join(", ", missing);

        var overlaps = await db.Appointments.AnyAsync(x => x.StylistId == stylistId &&
            x.AppointmentDate == date.Date && x.Status != "Cancelled" &&
            x.Status != "Rejected" && x.Status != "NoShow" &&
            x.StartTime < endTime && startTime < x.EndTime);
        return overlaps ? "Khung giờ đã chồng lấn với lịch khác của thợ." : null;
    }
}
