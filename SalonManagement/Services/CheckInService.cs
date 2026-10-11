using System.Data;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public class CheckInService(ApplicationDbContext db, TimeProvider clock, AppointmentNotificationBus? notifications = null)
{
    // Existing date/time columns contain salon local time; new event timestamps use UTC.
    public static DateTimeOffset StartsAt(Appointment a) =>
        new(DateTime.SpecifyKind(a.AppointmentDate.Date.Add(a.StartTime), DateTimeKind.Unspecified), TimeSpan.FromHours(7));
    public static bool CanCheckIn(Appointment a, DateTimeOffset now) =>
        a.Status == AppointmentStatuses.Confirmed && a.CheckedInAt == null &&
        now >= StartsAt(a).AddMinutes(-60) && now <= StartsAt(a).AddMinutes(60);
    public Task<Appointment> Apply(int id, string action, string actorId)
    {
        var attempts = 0;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
        {
            if (attempts++ > 0) db.ChangeTracker.Clear();
            return ApplyCore(id, action, actorId);
        });
    }
    private async Task<Appointment> ApplyCore(int id, string action, string actorId)
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
        StylistNotification? notification = null;
        switch (action)
        {
            case "check-in":
                if (!CanCheckIn(a, now)) throw new InvalidOperationException("Chỉ check-in lịch đã xác nhận trong khoảng 60 phút trước đến 60 phút sau giờ hẹn.");
                a.Status = AppointmentStatuses.Arrived;
                a.CheckedInAt = now;
                var delay = (now - StartsAt(a)).TotalMinutes;
                a.LateMinutes = delay > 15 ? (int)Math.Ceiling(delay) : 0;
                notification = new() { StylistId = a.StylistId, AppointmentId = id,
                    CreatedAt = now, Message = $"Khách {a.Customer.FullName} đã đến cho lịch hẹn #{id}." };
                db.StylistNotifications.Add(notification);
                break;
            default: throw new InvalidOperationException("Thao tác không hợp lệ.");
        }
        a.UpdatedAt = now.UtcDateTime;
        a.Version = Guid.NewGuid();
        db.AppointmentAudits.Add(new() { AppointmentId = id, ActorId = actorId, Action = action,
            PreviousStatus = previous, NewStatus = a.Status, OccurredAt = now });
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        if (notification != null) notifications?.Publish(notification);
        return a;
    }

}
