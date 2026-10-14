using System.Globalization;
using SalonManagement.Models;

namespace SalonManagement.Services;

/// <summary>
/// Builds read-only transfer suggestions from the salon's current work schedules,
/// booking intervals, service names/groups, and opening hours.
/// </summary>
public static class ScheduleCoordinationPlanner
{
    private static readonly TimeSpan SuggestionStep = TimeSpan.FromMinutes(15);
    private const int MaxSuggestionsPerStylist = 3;

    private static readonly HashSet<string> NonBlockingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed", "Cancelled", "Canceled", "NoShow", "No Show"
    };

    public static ScheduleCoordinationPlan BuildPlan(
        Appointment appointment,
        int unavailableStylistId,
        DateOnly date,
        IReadOnlyCollection<Stylist> stylists,
        IReadOnlyCollection<Appointment> dayAppointments,
        BusinessHourResponse businessHours)
    {
        var duration = GetDuration(appointment);
        var start = appointment.StartTime;
        var end = start + TimeSpan.FromMinutes(duration);
        var serviceNames = appointment.AppointmentServices
            .SelectMany(item => new[] { item.Service.ServiceName, item.Service.ServiceGroup?.GroupName })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        var options = stylists
            .Where(stylist => stylist.StylistId != unavailableStylistId)
            .OrderBy(stylist => stylist.FullName)
            .Select(stylist => Assess(
                stylist,
                appointment,
                date,
                duration,
                start,
                end,
                serviceNames,
                dayAppointments,
                businessHours))
            .ToArray();

        return new ScheduleCoordinationPlan(
            options,
            options.Any(option => option.AvailableAtOriginalTime || option.SuggestedStartTimes.Count > 0));
    }

    public static int GetDuration(Appointment appointment)
    {
        var reservedMinutes = Math.Max(0, (int)(appointment.EndTime - appointment.StartTime).TotalMinutes);
        var serviceMinutes = appointment.AppointmentServices.Sum(item => Math.Max(0, item.DurationMinutes));
        return Math.Max(reservedMinutes, serviceMinutes);
    }

    private static ScheduleCoordinationStylistOption Assess(
        Stylist stylist,
        Appointment appointment,
        DateOnly date,
        int durationMinutes,
        TimeSpan originalStart,
        TimeSpan originalEnd,
        IReadOnlyCollection<string> serviceNames,
        IReadOnlyCollection<Appointment> dayAppointments,
        BusinessHourResponse businessHours)
    {
        var reasons = GetSkillRejectionReasons(stylist, appointment).ToList();
        if (durationMinutes <= 0)
        {
            reasons.Add("Không xác định được thời lượng phục vụ của lịch hẹn.");
        }

        var shifts = stylist.WorkSchedules
            .Where(schedule => DateOnly.FromDateTime(schedule.WorkDate) == date &&
                string.Equals(schedule.Status, "Working", StringComparison.OrdinalIgnoreCase))
            .OrderBy(schedule => schedule.StartTime)
            .ToArray();

        if (shifts.Length == 0)
        {
            reasons.Add("Thợ chưa được xếp ca làm trong ngày này.");
        }
        else if (!businessHours.IsClosed && businessHours.OpensAt.HasValue && businessHours.ClosesAt.HasValue)
        {
            var withinBusinessHours = shifts.Any(shift =>
                shift.StartTime <= originalStart && shift.EndTime >= originalEnd &&
                originalStart >= businessHours.OpensAt.Value.ToTimeSpan() &&
                originalEnd <= businessHours.ClosesAt.Value.ToTimeSpan());
            if (!withinBusinessHours)
            {
                reasons.Add("Giờ hẹn nằm ngoài ca làm hoặc ngoài giờ mở cửa của salon.");
            }
        }
        else
        {
            reasons.Add("Salon đóng cửa trong ngày đã chọn.");
        }

        var collision = FindCollision(stylist.StylistId, appointment.AppointmentId,
            originalStart, originalEnd, dayAppointments);
        if (collision is not null)
        {
            reasons.Add($"Bị trùng với lịch hẹn #{collision.AppointmentId}.");
        }

        var availableAtOriginalTime = reasons.Count == 0;
        var suggestedTimes = new List<string>();
        if (durationMinutes > 0 && GetSkillRejectionReasons(stylist, appointment).Count == 0 && !businessHours.IsClosed)
        {
            foreach (var shift in shifts)
            {
                var windowStart = Max(shift.StartTime, businessHours.OpensAt?.ToTimeSpan() ?? TimeSpan.Zero);
                var windowEnd = Min(shift.EndTime, businessHours.ClosesAt?.ToTimeSpan() ?? TimeSpan.Zero);
                for (var candidateStart = AlignToNextStep(windowStart);
                     candidateStart + TimeSpan.FromMinutes(durationMinutes) <= windowEnd;
                     candidateStart += SuggestionStep)
                {
                    var candidateEnd = candidateStart + TimeSpan.FromMinutes(durationMinutes);
                    if (candidateStart == originalStart || FindCollision(stylist.StylistId,
                            appointment.AppointmentId, candidateStart, candidateEnd, dayAppointments) is not null)
                    {
                        continue;
                    }

                    suggestedTimes.Add(candidateStart.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
                    if (suggestedTimes.Count == MaxSuggestionsPerStylist)
                    {
                        break;
                    }
                }

                if (suggestedTimes.Count == MaxSuggestionsPerStylist)
                {
                    break;
                }
            }
        }

        // Include the service list in the assessment path so empty appointments cannot
        // accidentally receive a recommendation when data is missing.
        if (serviceNames.Count == 0 && !reasons.Contains("Lịch hẹn chưa có dịch vụ để đối chiếu kỹ năng."))
        {
            reasons.Add("Lịch hẹn chưa có dịch vụ để đối chiếu kỹ năng.");
            availableAtOriginalTime = false;
            suggestedTimes.Clear();
        }

        return new ScheduleCoordinationStylistOption(
            stylist.StylistId,
            stylist.FullName,
            availableAtOriginalTime,
            reasons,
            suggestedTimes);
    }

    public static IReadOnlyList<string> GetSkillRejectionReasons(Stylist stylist, Appointment appointment)
    {
        var requiredServices = appointment.AppointmentServices.ToArray();
        if (requiredServices.Length == 0)
        {
            return ["Lịch hẹn chưa có dịch vụ để đối chiếu kỹ năng."];
        }

        var skills = stylist.Services.Select(item => item.ServiceId).ToHashSet();
        if (skills.Count == 0)
        {
            return ["Thợ chưa được khai báo kỹ năng chuyên môn."];
        }

        var reasons = new List<string>();
        foreach (var item in requiredServices)
        {
            if (!skills.Contains(item.ServiceId))
                reasons.Add($"Chưa xác nhận kỹ năng phù hợp với dịch vụ “{item.Service.ServiceName}”.");
        }

        return reasons;
    }

    private static Appointment? FindCollision(
        int stylistId,
        int currentAppointmentId,
        TimeSpan start,
        TimeSpan end,
        IReadOnlyCollection<Appointment> appointments) => appointments.FirstOrDefault(existing =>
        existing.AppointmentId != currentAppointmentId &&
        existing.StylistId == stylistId &&
        !NonBlockingStatuses.Contains(existing.Status) &&
        start < existing.EndTime && end > existing.StartTime);

    private static TimeSpan AlignToNextStep(TimeSpan value)
    {
        var ticks = SuggestionStep.Ticks;
        return TimeSpan.FromTicks(((value.Ticks + ticks - 1) / ticks) * ticks);
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;
    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
}
