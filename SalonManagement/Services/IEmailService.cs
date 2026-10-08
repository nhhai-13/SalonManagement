namespace SalonManagement.Services;

/// <summary>Gửi email thông báo liên quan đến quản lý mật khẩu (AC2).</summary>
public interface IEmailService
{
    Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword);
    /// <summary>
    /// Gửi email chứa link đặt lại mật khẩu.
    /// Trong môi trường Development sẽ ghi log console thay vì gửi SMTP thật.
    /// </summary>
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);

    /// <summary>Gửi mã xác minh email gồm 6 chữ số.</summary>
    Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode);

    /// <summary>Gửi email thông báo dời lịch hoặc đổi thợ cho khách hàng (AC5).</summary>
    Task SendRescheduleNotificationEmailAsync(
        string toEmail,
        string customerName,
        string stylistName,
        DateTime appointmentDate,
        TimeSpan startTime,
        TimeSpan endTime,
        string servicesSummary,
        string? reason = null,
        string salonAddress = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh",
        string salonHotline = "1900 1234") => Task.CompletedTask;
}

