namespace SalonManagement.Services;

/// <summary>
/// Giới hạn số lần yêu cầu đặt lại mật khẩu qua email (AC4).
/// </summary>
public interface IPasswordResetRateLimiter
{
    /// <summary>
    /// Kiểm tra xem email này có còn được phép gửi yêu cầu reset mật khẩu không.
    /// </summary>
    /// <returns>true nếu còn trong ngưỡng cho phép; false nếu đã vượt quá giới hạn.</returns>
    bool TryRecord(string email);

    /// <summary>Trả về số lần request còn lại trong cửa sổ 1 giờ hiện tại.</summary>
    int GetRemainingAttempts(string email);
}
