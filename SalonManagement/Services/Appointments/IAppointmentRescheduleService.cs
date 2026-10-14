using SalonManagement.Models.ViewModels.Appointments;

namespace SalonManagement.Services.Appointments;

public interface IAppointmentRescheduleService
{
    /// <summary>
    /// Lấy dữ liệu lịch làm việc và lịch hẹn của salon trong ngày để hiển thị trên màn hình lịch ngày của lễ tân.
    /// </summary>
    Task<DailyScheduleDto> GetDailyScheduleAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy chi tiết lịch hẹn kèm danh sách thợ làm việc trong ngày và đánh giá kỹ năng để phục vụ Modal dời lịch.
    /// </summary>
    Task<AppointmentDetailForRescheduleDto?> GetRescheduleInfoAsync(int appointmentId, DateTime? targetDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra tính hợp lệ trước khi đổi thợ / dời giờ (kỹ năng F33, ca làm việc, trùng lấn lịch).
    /// </summary>
    Task<RescheduleValidationResult> ValidateRescheduleAsync(int appointmentId, int newStylistId, DateTime newDate, TimeSpan newStartTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Thực thi dời giờ / đổi thợ trong transaction an toàn, chống race condition.
    /// </summary>
    Task<RescheduleResult> RescheduleAppointmentAsync(int appointmentId, RescheduleAppointmentRequest request, string? currentUserId = null, string? currentUserName = null, CancellationToken cancellationToken = default);
}
