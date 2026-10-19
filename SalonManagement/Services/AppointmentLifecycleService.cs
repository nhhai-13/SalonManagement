using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record AppointmentLifecycleResult(bool Succeeded, string? Error)
{
    public static AppointmentLifecycleResult Success() => new(true, null);
    public static AppointmentLifecycleResult Rejected(string error) => new(false, error);
}

/// <summary>Applies reception-controlled appointment state changes with the time limits from Sprint 3.</summary>
public sealed class AppointmentLifecycleService
{
    private readonly ApplicationDbContext db;
    private readonly TimeProvider timeProvider;
    private readonly StylistInAppNotificationService notifications;

    [ActivatorUtilitiesConstructor]
    public AppointmentLifecycleService(ApplicationDbContext db, TimeProvider timeProvider, StylistInAppNotificationService notifications)
    {
        this.db = db;
        this.timeProvider = timeProvider;
        this.notifications = notifications;
    }

    // Keeps existing isolated tests and maintenance tools source-compatible.
    public AppointmentLifecycleService(ApplicationDbContext db, TimeProvider timeProvider)
        : this(db, timeProvider, new StylistInAppNotificationService(db, timeProvider)) { }

    public async Task<AppointmentLifecycleResult> CheckInAsync(int appointmentId)
    {
        var appointment = await db.Appointments.Include(item => item.Customer).Include(item => item.Invoice).SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return AppointmentLifecycleResult.Rejected("Không tìm thấy lịch hẹn.");
        if (appointment.Status != "Confirmed") return AppointmentLifecycleResult.Rejected("Chỉ có thể check-in lịch đã xác nhận.");

        var now = SalonClock.GetLocalNow(timeProvider);
        var scheduledAt = appointment.AppointmentDate.Date.Add(appointment.StartTime);
        if (now < scheduledAt.Subtract(TimeSpan.FromMinutes(60)) || now > scheduledAt.Add(TimeSpan.FromMinutes(60)))
            return AppointmentLifecycleResult.Rejected("Chỉ có thể check-in trong khoảng 60 phút trước hoặc sau giờ hẹn.");

        var lateMinutes = (int)Math.Floor((now - scheduledAt).TotalMinutes);
        appointment.Status = "Arrived";
        appointment.ArrivedAt = now;
        appointment.IsLate = lateMinutes > 15;
        appointment.LateMinutes = appointment.IsLate ? lateMinutes : 0;
        appointment.UpdatedAt = now;
        // A bill starts at check-in so reception can add walk-in services while the client is being served.
        if (appointment.Invoice is null)
        {
            var subtotal = await db.AppointmentServices.Where(item => item.AppointmentId == appointmentId).SumAsync(item => item.Price);
            db.Invoices.Add(new Invoice
            {
                AppointmentId = appointment.AppointmentId,
                Subtotal = subtotal,
                DiscountAmount = 0,
                TotalAmount = subtotal,
                Status = "Unpaid",
                CreatedAt = now
            });
        }
        var stylistEmail = await db.Stylists.Where(item => item.StylistId == appointment.StylistId).Select(item => item.Email).SingleOrDefaultAsync();
        notifications.Add(stylistEmail, appointment.AppointmentId, "CheckIn", $"Khách {appointment.CustomerNameOverride ?? appointment.Customer.FullName} đã check-in cho lịch {appointment.StartTime:hh\\:mm}.");
        await db.SaveChangesAsync();
        return AppointmentLifecycleResult.Success();
    }

    public async Task<AppointmentLifecycleResult> MarkNoShowAsync(int appointmentId, string? actorId)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return AppointmentLifecycleResult.Rejected("Không tìm thấy lịch hẹn.");
        if (appointment.Status != "Confirmed") return AppointmentLifecycleResult.Rejected("Chỉ có thể đánh dấu không đến cho lịch đã xác nhận.");

        var now = SalonClock.GetLocalNow(timeProvider);
        var scheduledAt = appointment.AppointmentDate.Date.Add(appointment.StartTime);
        if (now < scheduledAt.Add(TimeSpan.FromMinutes(30)))
            return AppointmentLifecycleResult.Rejected("Chỉ được đánh dấu không đến sau giờ hẹn ít nhất 30 phút.");

        appointment.Status = "NoShow";
        appointment.NoShowAt = now;
        appointment.NoShowByUserId = actorId;
        appointment.UpdatedAt = now;
        await db.SaveChangesAsync();
        return AppointmentLifecycleResult.Success();
    }

    public async Task<AppointmentLifecycleResult> UndoNoShowAsync(int appointmentId)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return AppointmentLifecycleResult.Rejected("Không tìm thấy lịch hẹn.");
        if (appointment.Status != "NoShow" || appointment.NoShowAt is null)
            return AppointmentLifecycleResult.Rejected("Lịch hẹn này không ở trạng thái không đến.");

        var now = SalonClock.GetLocalNow(timeProvider);
        if (now - appointment.NoShowAt.Value > TimeSpan.FromMinutes(15))
            return AppointmentLifecycleResult.Rejected("Chỉ có thể hoàn tác trong vòng 15 phút sau khi đánh dấu không đến.");

        appointment.Status = "Confirmed";
        appointment.NoShowAt = null;
        appointment.NoShowByUserId = null;
        appointment.UpdatedAt = now;
        await db.SaveChangesAsync();
        return AppointmentLifecycleResult.Success();
    }
}
