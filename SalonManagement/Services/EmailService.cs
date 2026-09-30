using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace SalonManagement.Services;

/// <summary>
/// Dịch vụ gửi email thông báo hệ thống và đặt lại mật khẩu (AC2).
/// Hỗ trợ gửi qua SMTP (Gmail) khi có cấu hình SmtpSettings, kèm fallback ghi log console.
/// </summary>
public sealed class EmailService(
    ILogger<EmailService> logger,
    IConfiguration configuration,
    IWebHostEnvironment env) : IEmailService
{
    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        var smtpSection = configuration.GetSection("SmtpSettings");
        var server = smtpSection["Server"];
        var portStr = smtpSection["Port"];
        var senderEmail = smtpSection["SenderEmail"];
        var senderName = smtpSection["SenderName"] ?? "Salon Management Support";
        var username = smtpSection["Username"];
        var password = smtpSection["Password"];

        int port = int.TryParse(portStr, out var p) ? p : 587;

        var hasSmtpConfig = !string.IsNullOrWhiteSpace(server)
            && !string.IsNullOrWhiteSpace(senderEmail)
            && !string.IsNullOrWhiteSpace(username)
            && !string.IsNullOrWhiteSpace(password);

        if (hasSmtpConfig)
        {
            try
            {
                using var client = new SmtpClient(server, port)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(username, password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail!, senderName),
                    Subject = "Đặt lại mật khẩu tài khoản Salon Management",
                    IsBodyHtml = true,
                    Body = BuildPasswordResetHtmlBody(toEmail, resetLink)
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                logger.LogInformation("Đã gửi email đặt lại mật khẩu thành công qua SMTP tới {Email}", toEmail);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi gửi email SMTP tới {Email}. Fallback ghi log link reset.", toEmail);
            }
        }
        else
        {
            logger.LogWarning("Chưa cấu hình đầy đủ SmtpSettings trong configuration. Fallback ghi log link reset.");
        }

        // Fallback ghi log ra console (đặc biệt hữu ích khi dev/staging hoặc khi mạng gặp sự cố)
        if (env.IsDevelopment() || env.IsStaging())
        {
            logger.LogWarning(
                "[FALLBACK EMAIL LOG] Gửi link đặt lại mật khẩu tới {Email}:\n{ResetLink}",
                toEmail, resetLink);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("════════════════════════════════════════");
            Console.WriteLine($"[FALLBACK EMAIL] Đặt lại mật khẩu cho: {toEmail}");
            Console.WriteLine($"[FALLBACK EMAIL] Link: {resetLink}");
            Console.WriteLine("════════════════════════════════════════");
            Console.ResetColor();
        }
    }

    private static string BuildPasswordResetHtmlBody(string recipientEmail, string resetLink)
    {
        return $"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Đặt lại mật khẩu</title>
</head>
<body style="margin: 0; padding: 0; background-color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1e293b;">
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color: #f8fafc; padding: 40px 15px;">
        <tr>
            <td align="center">
                <table role="presentation" width="100%" max-width="580px" cellspacing="0" cellpadding="0" style="max-width: 580px; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -2px rgba(0, 0, 0, 0.05); overflow: hidden; border: 1px solid #e2e8f0;">
                    <!-- Header -->
                    <tr>
                        <td style="background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%); padding: 32px 30px; text-align: center;">
                            <h1 style="margin: 0; color: #ffffff; font-size: 24px; font-weight: 700; letter-spacing: 0.5px;">
                                Salon Management
                            </h1>
                            <p style="margin: 6px 0 0 0; color: #94a3b8; font-size: 14px;">Hệ thống quản lý dịch vụ làm đẹp &amp; spa</p>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style="padding: 36px 32px;">
                            <h2 style="margin: 0 0 16px 0; color: #0f172a; font-size: 20px; font-weight: 600;">
                                Yêu cầu đặt lại mật khẩu
                            </h2>
                            <p style="margin: 0 0 16px 0; font-size: 15px; line-height: 1.6; color: #475569;">
                                Xin chào <strong>{recipientEmail}</strong>,
                            </p>
                            <p style="margin: 0 0 24px 0; font-size: 15px; line-height: 1.6; color: #475569;">
                                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn tại hệ thống Salon Management. Nhấn vào nút bên dưới để tiến hành thiết lập mật khẩu mới:
                            </p>
                            
                            <!-- Button CTA -->
                            <table role="presentation" cellspacing="0" cellpadding="0" style="margin: 28px 0; text-align: center; width: 100%;">
                                <tr>
                                    <td align="center">
                                        <a href="{resetLink}" target="_blank" style="display: inline-block; background-color: #2563eb; color: #ffffff; font-size: 15px; font-weight: 600; text-decoration: none; padding: 14px 32px; border-radius: 8px; box-shadow: 0 2px 4px rgba(37, 99, 235, 0.2);">
                                            Đặt lại mật khẩu
                                        </a>
                                    </td>
                                </tr>
                            </table>

                            <!-- Important Note Box -->
                            <div style="background-color: #fef3c7; border-left: 4px solid #f59e0b; padding: 14px 16px; border-radius: 4px; margin-bottom: 24px;">
                                <p style="margin: 0; font-size: 13px; line-height: 1.5; color: #92400e;">
                                    ⏱ <strong>Lưu ý:</strong> Link đặt lại mật khẩu này có thời hạn sử dụng trong <strong>30 phút</strong> và chỉ sử dụng được <strong>1 lần duy nhất</strong>.
                                </p>
                            </div>

                            <!-- Fallback Link -->
                            <p style="margin: 0 0 8px 0; font-size: 13px; line-height: 1.5; color: #64748b;">
                                Nếu nút bấm trên không hoạt động, bạn có thể sao chép và dán đường link sau vào trình duyệt:
                            </p>
                            <p style="margin: 0 0 24px 0; font-size: 12px; line-height: 1.5; word-break: break-all; color: #2563eb;">
                                <a href="{resetLink}" style="color: #2563eb; text-decoration: underline;">{resetLink}</a>
                            </p>

                            <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />

                            <!-- Security Disclaimer -->
                            <p style="margin: 0; font-size: 13px; line-height: 1.5; color: #94a3b8;">
                                🔒 Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email. Mật khẩu hiện tại của bạn vẫn an toàn và không bị thay đổi.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style="background-color: #f8fafc; padding: 20px 30px; text-align: center; border-top: 1px solid #e2e8f0;">
                            <p style="margin: 0; font-size: 12px; color: #94a3b8;">
                                © {DateTime.UtcNow.Year} Salon Management System. Mọi quyền được bảo lưu.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
""";
    }
}
