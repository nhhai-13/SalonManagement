using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record RescheduleAppointmentRequest(int StylistId, TimeSpan StartTime);
public sealed record AppointmentChangeActor(string Id, string Name);
public sealed record RescheduleAppointmentResult(bool Succeeded, string? ErrorCode, string? Error, Appointment? Appointment)
{
    public static RescheduleAppointmentResult Rejected(string code, string error) => new(false, code, error, null);
    public static RescheduleAppointmentResult Success(Appointment appointment) => new(true, null, null, appointment);
}

public sealed class AppointmentReschedulingService(ApplicationDbContext db, TimeProvider timeProvider)
{
    private static readonly string[] UnavailableStatuses = ["Cancelled", "Rejected", "NoShow", "Completed", "InProgress"];

    public async Task<RescheduleAppointmentResult> RescheduleAsync(int appointmentId, RescheduleAppointmentRequest request, AppointmentChangeActor? actor = null)
    {
        var appointment = await db.Appointments
            .Include(item => item.AppointmentServices)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId);
        if (appointment is null) return RescheduleAppointmentResult.Rejected("not_found", "Không tìm thấy lịch hẹn.");
        if (UnavailableStatuses.Contains(appointment.Status, StringComparer.Ordinal))
            return RescheduleAppointmentResult.Rejected("status_not_editable", "Lịch hẹn ở trạng thái hiện tại không thể sửa.");

        var stylist = await db.Stylists
            .Include(item => item.Services)
            .SingleOrDefaultAsync(item => item.StylistId == request.StylistId && item.IsActive);
        if (stylist is null) return RescheduleAppointmentResult.Rejected("stylist_unavailable", "Thợ được chọn không còn hoạt động.");

        var requiredServiceIds = appointment.AppointmentServices.Select(item => item.ServiceId).Distinct().ToList();
        var missingServices = await db.Services.AsNoTracking()
            .Where(service => requiredServiceIds.Contains(service.ServiceId) && !stylist.Services.Select(skill => skill.ServiceId).Contains(service.ServiceId))
            .Select(service => service.ServiceName)
            .ToListAsync();
        if (missingServices.Count > 0)
            return RescheduleAppointmentResult.Rejected("F33", $"F33: Thợ không thực hiện được dịch vụ: {string.Join(", ", missingServices)}.");

        var duration = appointment.AppointmentServices.Sum(item => item.DurationMinutes);
        if (duration <= 0) return RescheduleAppointmentResult.Rejected("invalid_duration", "Lịch hẹn không có thời lượng dịch vụ hợp lệ.");
        var endTime = request.StartTime.Add(TimeSpan.FromMinutes(duration));
        var shifts = await db.WorkSchedules.AsNoTracking()
            .Where(shift => shift.StylistId == stylist.StylistId && shift.WorkDate.Date == appointment.AppointmentDate.Date && shift.Status == "Working")
            .ToListAsync();
        if (!shifts.Any(shift => shift.StartTime <= request.StartTime && endTime <= shift.EndTime))
        {
            var shiftDescription = shifts.Count == 0 ? "không có ca làm trong ngày này" : string.Join("; ", shifts.Select(shift => $"{shift.StartTime:hh\\:mm}–{shift.EndTime:hh\\:mm}"));
            return RescheduleAppointmentResult.Rejected("outside_shift", $"Khung giờ mới phải nằm trọn trong ca làm của thợ ({shiftDescription}).");
        }

        var conflict = await db.Appointments.AsNoTracking()
            .Include(item => item.Customer)
            .Where(item => item.AppointmentId != appointment.AppointmentId && item.StylistId == stylist.StylistId && item.AppointmentDate.Date == appointment.AppointmentDate.Date && !UnavailableStatuses.Contains(item.Status))
            .FirstOrDefaultAsync(item => item.StartTime < endTime && request.StartTime < item.EndTime);
        if (conflict is not null)
            return RescheduleAppointmentResult.Rejected("overlap", $"Khung giờ bị chồng lấn với lịch của {conflict.Customer.FullName} lúc {conflict.StartTime:hh\\:mm}–{conflict.EndTime:hh\\:mm}.");

        var oldStylistId = appointment.StylistId;
        var oldStartTime = appointment.StartTime;
        var hasChanges = oldStylistId != stylist.StylistId || oldStartTime != request.StartTime;
        if (!hasChanges) return RescheduleAppointmentResult.Success(appointment);

        appointment.StylistId = stylist.StylistId;
        appointment.StartTime = request.StartTime;
        appointment.EndTime = endTime;
        appointment.UpdatedAt = SalonClock.GetLocalNow(timeProvider);
        var changedAt = appointment.UpdatedAt.Value;
        var changedBy = actor ?? new AppointmentChangeActor("system", "Hệ thống");
        db.AppointmentChangeLogs.Add(new AppointmentChangeLog
        {
            AppointmentId = appointment.AppointmentId,
            ActorId = changedBy.Id,
            ActorName = changedBy.Name,
            ChangedAt = changedAt,
            OldStylistId = oldStylistId == stylist.StylistId ? null : oldStylistId,
            NewStylistId = oldStylistId == stylist.StylistId ? null : stylist.StylistId,
            OldStartTime = oldStartTime == request.StartTime ? null : oldStartTime,
            NewStartTime = oldStartTime == request.StartTime ? null : request.StartTime
        });
        try
        {
            await db.SaveChangesAsync();
            return RescheduleAppointmentResult.Success(appointment);
        }
        catch (DbUpdateException)
        {
            return RescheduleAppointmentResult.Rejected("overlap", "Khung giờ này vừa được một lễ tân khác sử dụng. Vui lòng chọn giờ khác.");
        }
    }
}
