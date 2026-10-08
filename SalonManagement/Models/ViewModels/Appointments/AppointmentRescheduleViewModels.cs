namespace SalonManagement.Models.ViewModels.Appointments;

public class RescheduleAppointmentRequest
{
    public int NewStylistId { get; set; }
    public DateTime NewDate { get; set; }
    public TimeSpan NewStartTime { get; set; }
    public string? Reason { get; set; }
}

public class RescheduleValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> UnqualifiedServices { get; set; } = new();
    public TimeSpan? ConflictingStartTime { get; set; }
    public TimeSpan? ConflictingEndTime { get; set; }
    public string? ConflictingCustomerName { get; set; }

    public static RescheduleValidationResult Success() => new() { IsValid = true };

    public static RescheduleValidationResult FailF33(string stylistName, List<string> missingServices) => new()
    {
        IsValid = false,
        ErrorCode = "F33",
        ErrorMessage = $"Thợ {stylistName} không thực hiện được các dịch vụ đã chọn: {string.Join(", ", missingServices)}.",
        UnqualifiedServices = missingServices
    };

    public static RescheduleValidationResult FailOverlap(TimeSpan start, TimeSpan end, string? customerName) => new()
    {
        IsValid = false,
        ErrorCode = "OVERLAP",
        ErrorMessage = $"Khung giờ đã chọn ({start:hh\\:mm} - {end:hh\\:mm}) trùng lấn với lịch hẹn khác của thợ{(string.IsNullOrEmpty(customerName) ? "" : $" (Khách: {customerName})")}.",
        ConflictingStartTime = start,
        ConflictingEndTime = end,
        ConflictingCustomerName = customerName
    };

    public static RescheduleValidationResult FailOutOfShift(string stylistName, string shiftDescription) => new()
    {
        IsValid = false,
        ErrorCode = "OUT_OF_SHIFT",
        ErrorMessage = $"Khung giờ đã chọn nằm ngoài ca làm việc của thợ {stylistName}. {shiftDescription}"
    };

    public static RescheduleValidationResult Fail(string errorCode, string message) => new()
    {
        IsValid = false,
        ErrorCode = errorCode,
        ErrorMessage = message
    };
}

public class RescheduleResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public RescheduleValidationResult? Validation { get; set; }
    public AppointmentCalendarCardDto? UpdatedAppointment { get; set; }
}

public class AppointmentDetailForRescheduleDto
{
    public int AppointmentId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public int CurrentStylistId { get; set; }
    public string CurrentStylistName { get; set; } = string.Empty;
    public DateTime CurrentDate { get; set; }
    public TimeSpan CurrentStartTime { get; set; }
    public TimeSpan CurrentEndTime { get; set; }
    public int TotalDurationMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<AppointmentServiceItemDto> Services { get; set; } = new();
    public List<EligibleStylistDto> EligibleStylists { get; set; } = new();
}

public class AppointmentServiceItemDto
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
}

public class EligibleStylistDto
{
    public int StylistId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool HasAllSkills { get; set; }
    public List<string> MissingServices { get; set; } = new();
    public bool HasWorkingShift { get; set; }
    public List<ShiftWindowDto> Shifts { get; set; } = new();
}

public class ShiftWindowDto
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string FormattedWindow => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
}

public class DailyScheduleDto
{
    public DateTime Date { get; set; }
    public string FormattedDate { get; set; } = string.Empty;
    public TimeSpan SalonOpenTime { get; set; } = new(8, 0, 0);
    public TimeSpan SalonCloseTime { get; set; } = new(20, 0, 0);
    public bool IsClosed { get; set; }
    public List<StylistColumnDto> Stylists { get; set; } = new();
}

public class StylistColumnDto
{
    public int StylistId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? ProfileImagePath { get; set; }
    public List<ShiftWindowDto> Shifts { get; set; } = new();
    public List<AppointmentCalendarCardDto> Appointments { get; set; } = new();
}

public class AppointmentCalendarCardDto
{
    public int AppointmentId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int StylistId { get; set; }
    public string StylistName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string FormattedTimeRange => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = "Pending";
    public string ServiceNamesSummary { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public bool CanReschedule => Status != "Completed" && Status != "Cancelled";
}
