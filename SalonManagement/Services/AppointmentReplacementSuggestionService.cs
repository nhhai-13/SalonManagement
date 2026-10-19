using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed record AppointmentReplacementSuggestion(
    int AppointmentId,
    int? StylistId,
    string? StylistName,
    TimeSpan? StartTime,
    TimeSpan? EndTime,
    bool KeepsOriginalTime,
    string Message);

public sealed class AppointmentReplacementSuggestionService(ApplicationDbContext db, StylistAvailabilityService availability)
{
    public async Task<AppointmentReplacementSuggestion> SuggestAsync(int appointmentId, int absentStylistId)
    {
        var appointment = await db.Appointments.AsNoTracking()
            .Include(item => item.AppointmentServices)
            .SingleOrDefaultAsync(item => item.AppointmentId == appointmentId && item.StylistId == absentStylistId);
        if (appointment is null)
            return new(appointmentId, null, null, null, null, false, "Không tìm thấy lịch của thợ đã chọn.");

        var serviceIds = appointment.AppointmentServices.Select(item => item.ServiceId).Distinct().ToArray();
        if (serviceIds.Length == 0)
            return new(appointmentId, null, null, null, null, false, "Lịch hẹn chưa có dịch vụ để tìm thợ thay thế.");

        var date = DateOnly.FromDateTime(appointment.AppointmentDate);
        var originalStart = TimeOnly.FromTimeSpan(appointment.StartTime);
        var skilledStylists = await availability.GetStylistsAsync(serviceIds);
        if (!skilledStylists.Any(stylist => stylist.Id != absentStylistId))
            return new(appointmentId, null, null, null, null, false, "Không có thợ thay thế nào đủ kỹ năng thực hiện toàn bộ dịch vụ.");

        var directReplacement = await availability.AssignAsync(serviceIds, StylistAvailabilityService.AnyStylist, date, originalStart, absentStylistId);
        if (directReplacement is not null)
            return ToSuggestion(appointmentId, directReplacement, true, "Có thợ thay thế phù hợp ở giờ cũ.");

        var alternativeSlots = await availability.GetSlotsAsync(serviceIds, StylistAvailabilityService.AnyStylist, date);
        foreach (var slot in alternativeSlots
            .Select(slot => new { Slot = slot, Start = TimeOnly.Parse(slot.Start, System.Globalization.CultureInfo.InvariantCulture) })
            .Where(item => item.Start != originalStart)
            .OrderBy(item => Math.Abs((item.Start - originalStart).TotalMinutes))
            .ThenBy(item => item.Start < originalStart ? 1 : 0)
            .ThenBy(item => item.Start))
        {
            var replacement = await availability.AssignAsync(serviceIds, StylistAvailabilityService.AnyStylist, date, slot.Start, absentStylistId);
            if (replacement is not null)
                return ToSuggestion(appointmentId, replacement, false, "Không còn thợ rảnh ở giờ cũ; đã đề xuất giờ trống gần nhất.");
        }

        return new(appointmentId, null, null, null, null, false, "Các thợ có đủ kỹ năng không còn khung giờ trống phù hợp.");
    }

    private static AppointmentReplacementSuggestion ToSuggestion(int appointmentId, StylistAssignment assignment, bool keepsOriginalTime, string message) => new(
        appointmentId,
        assignment.StylistId,
        assignment.Name,
        TimeSpan.Parse(assignment.Start, System.Globalization.CultureInfo.InvariantCulture),
        TimeSpan.Parse(assignment.End, System.Globalization.CultureInfo.InvariantCulture),
        keepsOriginalTime,
        message);
}
