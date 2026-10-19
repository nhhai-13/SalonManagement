namespace SalonManagement.Services;

public sealed record BatchRescheduleItem(int AppointmentId, int StylistId, TimeSpan StartTime);
public sealed record BatchRescheduleSucceeded(int AppointmentId, int StylistId, TimeSpan StartTime, TimeSpan EndTime, string NotificationStatus);
public sealed record BatchRescheduleFailed(int AppointmentId, string? ErrorCode, string Error);
public sealed record BatchRescheduleResult(IReadOnlyList<BatchRescheduleSucceeded> Succeeded, IReadOnlyList<BatchRescheduleFailed> Failed);

public sealed class BatchAppointmentReschedulingService(AppointmentReschedulingService rescheduling)
{
    public async Task<BatchRescheduleResult> RescheduleAsync(IEnumerable<BatchRescheduleItem> items, AppointmentChangeActor actor)
    {
        var succeeded = new List<BatchRescheduleSucceeded>();
        var failed = new List<BatchRescheduleFailed>();
        var seenAppointments = new HashSet<int>();
        foreach (var item in items)
        {
            if (item.AppointmentId <= 0 || item.StylistId <= 0 || item.StartTime < TimeSpan.Zero || item.StartTime >= TimeSpan.FromDays(1))
            {
                failed.Add(new(item.AppointmentId, "invalid_request", "Phương án chuyển lịch không hợp lệ."));
                continue;
            }
            if (!seenAppointments.Add(item.AppointmentId))
            {
                failed.Add(new(item.AppointmentId, "duplicate", "Lịch hẹn được chọn nhiều lần trong cùng một lô."));
                continue;
            }

            // Each invocation saves independently; a failed move or notification never rolls back earlier successful moves.
            var result = await rescheduling.RescheduleAsync(item.AppointmentId,
                new RescheduleAppointmentRequest(item.StylistId, item.StartTime), actor, queueCustomerEmail: true);
            if (!result.Succeeded)
            {
                failed.Add(new(item.AppointmentId, result.ErrorCode, result.Error ?? "Không thể chuyển lịch hẹn."));
                continue;
            }

            var appointment = result.Appointment!;
            succeeded.Add(new(appointment.AppointmentId, appointment.StylistId, appointment.StartTime, appointment.EndTime,
                result.NotificationStatus ?? "NotRequired"));
        }
        return new(succeeded, failed);
    }
}
