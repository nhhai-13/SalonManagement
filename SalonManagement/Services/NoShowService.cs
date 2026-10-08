using System.Data;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public class NoShowService(ApplicationDbContext db, TimeProvider clock)
{
    // Existing date/time columns contain salon local time; new event timestamps use UTC.
    public static DateTimeOffset StartsAt(Appointment a) =>
        new(DateTime.SpecifyKind(a.AppointmentDate.Date.Add(a.StartTime), DateTimeKind.Unspecified), TimeSpan.FromHours(7));
    public static bool CanMarkNoShow(Appointment a, DateTimeOffset now) =>
        (a.Status == AppointmentStatuses.Pending || a.Status == AppointmentStatuses.Confirmed) &&
        a.CheckedInAt == null && now > StartsAt(a).AddMinutes(30);
    public static bool CanUndo(Appointment a, DateTimeOffset now) =>
        a.Status == AppointmentStatuses.NoShow && a.NoShowAt.HasValue &&
        now >= a.NoShowAt && now <= a.NoShowAt.Value.AddMinutes(15);

    public async Task<Appointment> Apply(int id, string action, string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId)) throw new InvalidOperationException("Không xác định được người thực hiện.");
        var stylistId = await db.Appointments.AsNoTracking().Where(a => a.AppointmentId == id).Select(a => (int?)a.StylistId).SingleOrDefaultAsync()
            ?? throw new KeyNotFoundException("Không tìm thấy lịch hẹn.");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT StylistId FROM Stylists WITH (UPDLOCK, HOLDLOCK) WHERE StylistId = {stylistId}");
        var a = await db.Appointments.Include(x => x.Customer).SingleOrDefaultAsync(x => x.AppointmentId == id)
            ?? throw new KeyNotFoundException("Không tìm thấy lịch hẹn.");
        if (a.StylistId != stylistId) throw new InvalidOperationException("Thợ phụ trách vừa thay đổi. Vui lòng tải lại.");
        var now = clock.GetUtcNow();
        var previous = a.Status;
        switch (action)
        {
            case "no-show":
                if (!CanMarkNoShow(a, now)) throw new InvalidOperationException("Chỉ đánh dấu không đến khi quá giờ hẹn 30 phút và khách chưa check-in.");
                a.StatusBeforeNoShow = previous;
                a.Status = AppointmentStatuses.NoShow;
                a.NoShowAt = now;
                break;
            case "undo-no-show":
                if (!CanUndo(a, now)) throw new InvalidOperationException("Chỉ được hoàn tác không đến trong vòng 15 phút.");
                // A released slot may have been rebooked. Never restore an overlapping appointment.
                var occupied = await db.Appointments.Where(x => x.AppointmentId != id && x.StylistId == a.StylistId &&
                    x.AppointmentDate >= a.AppointmentDate.Date && x.AppointmentDate < a.AppointmentDate.Date.AddDays(1) &&
                    x.Status != AppointmentStatuses.NoShow && x.Status != AppointmentStatuses.Cancelled).ToListAsync();
                if (occupied.Any(x => x.StartTime < a.EndTime && x.EndTime > a.StartTime))
                    throw new InvalidOperationException("Khung giờ đã có lịch khác. Không thể hoàn tác gây trùng lịch.");
                a.Status = a.StatusBeforeNoShow ?? AppointmentStatuses.Confirmed;
                a.NoShowAt = null;
                a.StatusBeforeNoShow = null;
                break;
            default: throw new InvalidOperationException("Thao tác không hợp lệ.");
        }
        a.UpdatedAt = now.UtcDateTime;
        a.Version = Guid.NewGuid();
        db.AppointmentAudits.Add(new() { AppointmentId = id, ActorId = actorId, Action = action,
            PreviousStatus = previous, NewStatus = a.Status, OccurredAt = now });
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        return a;
    }

    public Task<List<Appointment>> OccupiedSlots(int stylistId, DateTime day) => db.Appointments.AsNoTracking()
        .Where(a => a.StylistId == stylistId && a.AppointmentDate >= day.Date && a.AppointmentDate < day.Date.AddDays(1) &&
            a.Status != AppointmentStatuses.NoShow && a.Status != AppointmentStatuses.Cancelled).ToListAsync();

    // Derived from current status, so undo and duplicate requests cannot inflate the count.
    public Task<int> NoShowCount(string phone) => db.Appointments.CountAsync(a =>
        a.Customer.Phone == phone && a.Status == AppointmentStatuses.NoShow);
}
