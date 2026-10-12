using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record AppointmentLifecycleResult(bool Succeeded, string? Error)
{
    public static AppointmentLifecycleResult Success() => new(true, null);
    public static AppointmentLifecycleResult Rejected(string error) => new(false, error);
}

/// <summary>Applies reception-controlled appointment state changes with the time limits from Sprint 3.</summary>
public sealed class AppointmentLifecycleService(ApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<AppointmentLifecycleResult> CheckInAsync(int appointmentId)
    {
        var appointment = await db.Appointments.Include(item => item.Customer).SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
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
        appointment.LateMinutes = appointment.IsLate ? lateMinutes : null;
        appointment.UpdatedAt = now;
        var stylistEmail = await db.Stylists.Where(item => item.StylistId == appointment.StylistId).Select(item => item.Email).SingleOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(stylistEmail))
        {
            db.StaffNotifications.Add(new StaffNotification
            {
                RecipientEmail = stylistEmail,
                AppointmentId = appointment.AppointmentId,
                Type = "CheckIn",
                Message = $"Khách {appointment.CustomerNameOverride ?? appointment.Customer.FullName} đã check-in cho lịch {appointment.StartTime:hh\\:mm}."
            });
        }
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
