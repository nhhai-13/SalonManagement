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
}
