namespace SalonManagement.Services;

/// <summary>
/// Triển khai gửi email cho môi trường Development (Sprint 1).
/// Ghi reset link ra ILogger/console thay vì SMTP thật để tiện test và Sprint Review demo.
/// TODO (Sprint 2): Thay bằng SMTP thật qua SmtpClient / MailKit + config appsettings.
/// </summary>
public sealed class EmailService(ILogger<EmailService> logger, IWebHostEnvironment env) : IEmailService
{
    public Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        if (env.IsDevelopment() || env.IsStaging())
        {
            // Dev/Staging: ghi ra console để tester dễ thao tác
            logger.LogWarning(
                "[DEV EMAIL] Gửi link đặt lại mật khẩu tới {Email}:\n{ResetLink}",
                toEmail, resetLink);

            // In ra stdout để hiển thị rõ ràng trong terminal khi demo Sprint Review
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine($"[DEV] Đặt lại mật khẩu cho: {toEmail}");
            Console.WriteLine($"[DEV] Link: {resetLink}");
            Console.WriteLine("════════════════════════════════════════");
            Console.ResetColor();

            return Task.CompletedTask;
        }

        // Production: TODO — tích hợp SMTP thật (Sprint 2+)
        logger.LogError(
            "EmailService chưa cấu hình SMTP production. Email tới {Email} không được gửi.", toEmail);
        return Task.CompletedTask;
    }
}
