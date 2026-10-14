using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed record BusinessHourResponse(
    DayOfWeek DayOfWeek,
    bool IsClosed,
    TimeOnly? OpensAt,
    TimeOnly? ClosesAt,
    string TimeZoneId);

public sealed record UpdateBusinessHourRequest(
    [Range(0, 6)] int DayOfWeek,
    bool IsClosed,
    TimeOnly? OpensAt,
    TimeOnly? ClosesAt);

public sealed record UpdateBusinessHoursRequest(
    [Required, MinLength(7), MaxLength(7)] IReadOnlyList<UpdateBusinessHourRequest> Days);
