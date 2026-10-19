using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

/// <summary>
/// Dịch vụ gửi email thông báo hệ thống và đặt lại mật khẩu (AC2).
/// Hỗ trợ gửi qua SMTP (Gmail) khi có cấu hình SmtpSettings, kèm fallback ghi log console.
/// </summary>
public sealed class EmailService(
    ILogger<EmailService> logger,
    IConfiguration configuration,
    IWebHostEnvironment env,
    ApplicationDbContext db) : IEmailService
{
    public Task SendBookingConfirmationEmailAsync(string toEmail, string customerName, string bookingReference,
        string servicesSummary, string stylistName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime,
        string salonAddress = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh") =>
        SendAppointmentNoticeAsync("booking-confirmation", "Xác nhận lịch hẹn tại Luminol Salon", "Lịch hẹn của bạn đã được ghi nhận",
            toEmail, customerName, bookingReference, servicesSummary, stylistName, appointmentDate, startTime, endTime, salonAddress);

    public Task SendAppointmentReminderEmailAsync(string toEmail, string customerName, string bookingReference,
        string servicesSummary, string stylistName, DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime,
        string salonAddress = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh") =>
        SendAppointmentNoticeAsync("appointment-reminder", "Nhắc lịch hẹn tại Luminol Salon", "Nhắc bạn sắp đến lịch hẹn",
            toEmail, customerName, bookingReference, servicesSummary, stylistName, appointmentDate, startTime, endTime, salonAddress);

    private async Task SendAppointmentNoticeAsync(string template, string subject, string heading,
        string toEmail, string customerName, string bookingReference, string servicesSummary, string stylistName,
        DateTime appointmentDate, TimeSpan startTime, TimeSpan endTime, string salonAddress)
    {
        var smtp = configuration.GetSection("SmtpSettings");
        var server = smtp["Server"];
        var sender = smtp["SenderEmail"];
        var username = smtp["Username"];
        var password = smtp["Password"];
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(sender) ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            if (env.IsDevelopment())
            {
                logger.LogInformation("[FALLBACK {Template}] to {Email}; reference {Reference}; {Date:dd/MM/yyyy} {Start:hh\\:mm}.",
                    template, toEmail, bookingReference, appointmentDate, startTime);
                return;
            }
            throw new InvalidOperationException("Chưa cấu hình SMTP để gửi email lịch hẹn.");
        }

        var html = $"""<html lang="vi"><body><h2>{WebUtility.HtmlEncode(heading)}</h2><p>Chào {WebUtility.HtmlEncode(customerName)},</p><p>Mã tra cứu: <strong>{WebUtility.HtmlEncode(bookingReference)}</strong></p><p>Dịch vụ: {WebUtility.HtmlEncode(servicesSummary)}</p><p>Nhân viên: {WebUtility.HtmlEncode(stylistName)}</p><p>Thời gian: {appointmentDate:dd/MM/yyyy}, {startTime.ToString(@"hh\:mm")}–{endTime.ToString(@"hh\:mm")}</p><p>Địa chỉ salon: {WebUtility.HtmlEncode(salonAddress)}</p></body></html>""";
        using var client = new SmtpClient(server, smtp.GetValue<int>("Port", 587))
        {
            EnableSsl = true, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password), Timeout = 15000
        };
        using var message = new MailMessage
        {
            From = new MailAddress(sender, smtp["SenderName"] ?? "Luminol Salon"),
            Subject = subject, IsBodyHtml = true, Body = html
        };
        message.To.Add(toEmail);
        await client.SendMailAsync(message);
    }

    public async Task SendAppointmentChangeEmailAsync(string toEmail, string stylistName, DateTime appointmentDate, TimeSpan startTime)
    {
        if (await LogStagingDelivery(toEmail, "appointment-change")) return;
        var smtp = configuration.GetSection("SmtpSettings");
        if (string.IsNullOrWhiteSpace(smtp["Server"]) || string.IsNullOrWhiteSpace(smtp["SenderEmail"]))
        {
            if (env.IsDevelopment() || env.IsStaging())
            {
                logger.LogInformation("[FALLBACK EMAIL] Lịch hẹn của {Email} đã đổi: thợ {Stylist}, {Date:dd/MM/yyyy} {Time:hh\\:mm}.", toEmail, stylistName, appointmentDate, startTime);
                return;
            }
            throw new InvalidOperationException("Chưa cấu hình SMTP để gửi email đổi lịch.");
        }

        using var client = new SmtpClient(smtp["Server"], smtp.GetValue<int>("Port", 587))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(smtp["Username"], smtp["Password"]),
            Timeout = 15000
        };
        using var message = new MailMessage(smtp["SenderEmail"]!, toEmail)
        {
            Subject = "Luminol Salon: lịch hẹn của bạn đã thay đổi",
            Body = $"Xin chào,\n\nLịch hẹn của bạn đã được cập nhật.\nThợ phụ trách: {stylistName}\nThời gian mới: {appointmentDate:dd/MM/yyyy} lúc {startTime:hh\\:mm}.\n\nNếu cần hỗ trợ, vui lòng liên hệ Luminol Salon."
        };
        await client.SendMailAsync(message);
    }

    public async Task SendServiceReviewEmailAsync(string toEmail, string customerName, string reviewLink, DateTime expiresAt)
    {
        var smtp = configuration.GetSection("SmtpSettings");
        var server = smtp["Server"];
        var senderEmail = smtp["SenderEmail"];
        var senderName = smtp["SenderName"] ?? "Luminol Salon & Spa";
        var username = smtp["Username"];
        var password = smtp["Password"];
        var port = smtp.GetValue<int>("Port", 587);
        var hasSmtpConfig = !string.IsNullOrWhiteSpace(server)
            && !string.IsNullOrWhiteSpace(senderEmail)
            && !string.IsNullOrWhiteSpace(username)
            && !string.IsNullOrWhiteSpace(password);

        if (hasSmtpConfig)
        {
            using var client = new SmtpClient(server, port)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };
            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail!, senderName),
                Subject = "Luminol Salon: mời bạn đánh giá dịch vụ",
                IsBodyHtml = true,
                Body = BuildServiceReviewHtmlBody(customerName, reviewLink, expiresAt)
            };
            message.To.Add(toEmail);
            await client.SendMailAsync(message);
            logger.LogInformation("Đã gửi liên kết đánh giá dịch vụ tới {Email}", toEmail);
            return;
        }

        logger.LogWarning("[FALLBACK EMAIL] Mời {Email} đánh giá dịch vụ trước {ExpiresAt:dd/MM/yyyy}: {ReviewLink}", toEmail, expiresAt, reviewLink);
        if (env.IsDevelopment() || env.IsStaging())
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[FALLBACK EMAIL] Đánh giá dịch vụ cho {toEmail}: {reviewLink}");
            Console.ResetColor();
        }
    }

    public async Task SendAppointmentNotificationEmailAsync(string toEmail, string customerName, string reference, string services, string stylistName, DateTime appointmentDate, TimeSpan startTime, bool isReminder)
    {
        var smtp = configuration.GetSection("SmtpSettings");
        var server = smtp["Server"];
        var senderEmail = smtp["SenderEmail"];
        var senderName = smtp["SenderName"] ?? "Luminol Salon & Spa";
        var username = smtp["Username"];
        var password = smtp["Password"];
        var hasSmtpConfig = !string.IsNullOrWhiteSpace(server) && !string.IsNullOrWhiteSpace(senderEmail) && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
        if (!hasSmtpConfig)
        {
            logger.LogWarning("[FALLBACK EMAIL] {Kind} lịch {Reference} gửi tới {Email}", isReminder ? "Nhắc" : "Xác nhận", reference, toEmail);
            return;
        }

        using var client = new SmtpClient(server, smtp.GetValue<int>("Port", 587))
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };
        using var message = new MailMessage
        {
            From = new MailAddress(senderEmail!, senderName),
            Subject = $"[Luminol Salon] {(isReminder ? "Nhắc lịch hẹn" : "Xác nhận lịch hẹn")} #{reference}",
            IsBodyHtml = true,
            Body = BuildAppointmentNotificationHtmlBody(customerName, reference, services, stylistName, appointmentDate, startTime, isReminder)
        };
        message.To.Add(toEmail);
        await client.SendMailAsync(message);
    }

    public async Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword)
    {
        if (await LogStagingDelivery(toEmail, "temporary-password")) return;
        var smtp = configuration.GetSection("SmtpSettings");
        var server = smtp["Server"];
        var senderEmail = smtp["SenderEmail"];
        var username = smtp["Username"];
        var password = smtp["Password"];
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(senderEmail) ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Cấu hình Gmail chưa đầy đủ. Hãy thêm SenderEmail, Username và App Password vào User Secrets.");
        using var client = new SmtpClient(smtp["Server"], smtp.GetValue<int>("Port", 587))
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(username, password),
            Timeout = 15000
        };
        using var message = new MailMessage(senderEmail, toEmail)
        {
            Subject = "Tài khoản Salon Management của bạn",
            Body = $"Tài khoản: {toEmail}\nMật khẩu tạm: {temporaryPassword}\nĐăng nhập tại trang đăng nhập nội bộ của salon. Bạn phải đổi mật khẩu ngay lần đăng nhập đầu tiên."
        };
        await client.SendMailAsync(message);
    }

    public async Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode)
    {
        if (await LogStagingDelivery(toEmail, "email-verification")) return;
        var smtpSection = configuration.GetSection("SmtpSettings");
        var server = smtpSection["Server"];
        var senderEmail = smtpSection["SenderEmail"];
        var senderName = smtpSection["SenderName"] ?? "Luminol Salon";
        var username = smtpSection["Username"];
        var password = smtpSection["Password"];
        var port = int.TryParse(smtpSection["Port"], out var configuredPort) ? configuredPort : 587;
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
                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail!, senderName),
                    Subject = $"{verificationCode} là mã xác minh email Luminol Salon",
                    IsBodyHtml = true,
                    Body = BuildEmailVerificationHtmlBody(toEmail, verificationCode)
                };
                message.To.Add(toEmail);
                await client.SendMailAsync(message);
                logger.LogInformation("Đã gửi mã xác minh email tới {Email}", toEmail);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Không thể gửi mã xác minh email tới {Email}", toEmail);
                if (!env.IsDevelopment() && !env.IsStaging()) throw;
            }
        }
        else if (!env.IsDevelopment() && !env.IsStaging())
        {
            throw new InvalidOperationException("Chưa cấu hình SMTP để gửi mã xác minh email.");
        }

        logger.LogWarning("[FALLBACK EMAIL] Mã xác minh cho {Email}: {Code}", toEmail, verificationCode);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[FALLBACK EMAIL] Mã xác minh cho {toEmail}: {verificationCode}");
        Console.ResetColor();
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        if (await LogStagingDelivery(toEmail, "password-reset")) return;
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

    private static string BuildServiceReviewHtmlBody(string customerName, string reviewLink, DateTime expiresAt) => $"""
<!DOCTYPE html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Đánh giá dịch vụ</title></head>
<body style="margin:0;padding:32px 14px;background:#f5f8fc;font-family:Arial,sans-serif;color:#1d2a44">
  <div style="max-width:560px;margin:auto;background:#fff;border:1px solid #dbe5f1;border-radius:14px;overflow:hidden">
    <div style="padding:26px 30px;background:#2864df;color:#fff"><h1 style="margin:0;font-size:23px">Luminol Salon &amp; Spa</h1><p style="margin:7px 0 0;color:#dbe8ff">Cảm ơn bạn đã sử dụng dịch vụ</p></div>
    <div style="padding:28px 30px"><p>Xin chào <strong>{WebUtility.HtmlEncode(customerName)}</strong>,</p><p>Chỉ mất khoảng 10 giây để chấm điểm và góp ý cho thợ đã phục vụ bạn. Phản hồi của bạn giúp tiệm cải thiện mỗi ngày.</p>
      <p style="margin:24px 0;text-align:center"><a href="{WebUtility.HtmlEncode(reviewLink)}" style="display:inline-block;padding:13px 22px;border-radius:8px;background:#2864df;color:#fff;text-decoration:none;font-weight:bold">Đánh giá dịch vụ</a></p>
      <p style="padding:12px 14px;border-radius:8px;background:#fff4cf;color:#8b6100;font-size:13px">⏳ Liên kết dùng một lần và có hiệu lực đến hết ngày {expiresAt:dd/MM/yyyy}.</p>
    </div>
  </div>
</body></html>
""";

    private static string BuildAppointmentNotificationHtmlBody(string customerName, string reference, string services, string stylistName, DateTime appointmentDate, TimeSpan startTime, bool isReminder) => $"""
<!DOCTYPE html>
<html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Lịch hẹn Luminol Salon</title></head>
<body style="margin:0;padding:32px 14px;background:#f3f6fb;font-family:Arial,sans-serif;color:#202f49">
  <div style="max-width:600px;margin:auto;background:#fff;border:1px solid #d6e0ed;border-radius:14px;overflow:hidden">
    <div style="padding:26px 30px;background:#1f2d45;color:#fff"><h1 style="margin:0;font-size:22px">Luminol Salon &amp; Spa</h1><p style="margin:7px 0 0;color:#9ec8ff;font-size:13px">{(isReminder ? "NHẮC LỊCH HẸN TỰ ĐỘNG" : "XÁC NHẬN LỊCH HẸN")}</p></div>
    <div style="padding:26px 30px"><p>Xin chào <strong>{WebUtility.HtmlEncode(customerName)}</strong>,</p><p>{(isReminder ? "Luminol Salon xin nhắc bạn về lịch hẹn sắp tới." : "Lịch hẹn của bạn đã được tiệm xác nhận thành công.")}</p>
      <div style="margin:20px 0;padding:14px 16px;border-radius:8px;background:#dbeafe;color:#173b88"><strong>MÃ TRA CỨU LỊCH HẸN: #{WebUtility.HtmlEncode(reference)}</strong></div>
      <table style="width:100%;border-collapse:collapse;font-size:14px"><tr><td style="padding:8px 0;color:#64748b">Dịch vụ đăng ký</td><td style="padding:8px 0;font-weight:bold">{WebUtility.HtmlEncode(services)}</td></tr><tr><td style="padding:8px 0;color:#64748b">Thợ phụ trách</td><td style="padding:8px 0;font-weight:bold">{WebUtility.HtmlEncode(stylistName)}</td></tr><tr><td style="padding:8px 0;color:#64748b">Giờ bắt đầu</td><td style="padding:8px 0;font-weight:bold">{appointmentDate:dd/MM/yyyy} lúc {startTime:hh\\:mm}</td></tr><tr><td style="padding:8px 0;color:#64748b">Địa chỉ tiệm</td><td style="padding:8px 0;font-weight:bold">Luminol Salon &amp; Spa · TP. Hồ Chí Minh</td></tr></table>
      <div style="margin-top:18px;padding:12px 14px;border-radius:8px;background:#fff3cd;color:#8a5a00;font-size:13px">⏳ Hệ thống nhắc lịch trước 24 giờ; nếu lịch hẹn bị hủy hoặc đổi giờ, lời nhắc cũ sẽ tự động được hủy.</div>
    </div>
  </div>
</body></html>
""";

    private static string BuildEmailVerificationHtmlBody(string recipientEmail, string verificationCode) => $"""
<!DOCTYPE html>
<html lang="vi">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Xác minh email</title></head>
<body style="margin:0;padding:40px 15px;background:#f8f5f0;font-family:Arial,sans-serif;color:#2d2521">
  <div style="max-width:560px;margin:auto;background:#fffdf9;border:1px solid #ded7cf;border-radius:12px;overflow:hidden">
    <div style="padding:28px;background:#704a37;color:#fff;text-align:center"><h1 style="margin:0;font-size:24px">Luminol Salon</h1></div>
    <div style="padding:32px;text-align:center">
      <h2 style="margin-top:0">Xác minh địa chỉ email</h2>
      <p>Mã xác minh dành cho <strong>{WebUtility.HtmlEncode(recipientEmail)}</strong> là:</p>
      <div style="margin:24px 0;font-size:34px;font-weight:700;letter-spacing:10px;color:#704a37">{verificationCode}</div>
      <p style="color:#756b65">Mã có hiệu lực trong 10 phút. Không chia sẻ mã này với bất kỳ ai.</p>
      <p style="color:#756b65">Nếu bạn không đăng ký tài khoản, hãy bỏ qua email này.</p>
    </div>
  </div>
</body>
</html>
""";

    public async Task SendRescheduleNotificationEmailAsync(
        string toEmail,
        string customerName,
        string stylistName,
        DateTime appointmentDate,
        TimeSpan startTime,
        TimeSpan endTime,
        string servicesSummary,
        string? reason = null,
        string salonAddress = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh",
        string salonHotline = "1900 1234")
    {
        if (await LogStagingDelivery(toEmail, "reschedule")) return;
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

        var htmlBody = BuildRescheduleNotificationHtmlBody(
            customerName,
            stylistName,
            appointmentDate,
            startTime,
            endTime,
            servicesSummary,
            reason,
            salonAddress,
            salonHotline);

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
                    Subject = $"[Salon Management] Cập nhật lịch hẹn của quý khách - {appointmentDate:dd/MM/yyyy}",
                    IsBodyHtml = true,
                    Body = htmlBody
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                logger.LogInformation("Đã gửi email thông báo dời lịch hẹn thành công qua SMTP tới {Email}", toEmail);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi gửi email thông báo dời lịch qua SMTP tới {Email}", toEmail);
                if (!env.IsDevelopment() && !env.IsStaging()) throw;
            }
        }
        else if (!env.IsDevelopment() && !env.IsStaging())
        {
            throw new InvalidOperationException("Chưa cấu hình SMTP để gửi email thông báo dời lịch.");
        }

        logger.LogWarning("[FALLBACK EMAIL] Thông báo dời lịch gửi tới {Email} (Thợ: {Stylist}, Ngày: {Date:dd/MM/yyyy}, Giờ: {Start:hh\\:mm}-{End:hh\\:mm})",
            toEmail, stylistName, appointmentDate, startTime, endTime);
    }

    public async Task SendReviewRequestEmailAsync(string toEmail, string customerName, string stylistName, string reviewUrl)
    {
        if (await LogStagingDelivery(toEmail, "review-request", reviewUrl)) return;
        var smtp = configuration.GetSection("SmtpSettings");
        var server = smtp["Server"];
        var sender = smtp["SenderEmail"];
        var username = smtp["Username"];
        var password = smtp["Password"];
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(sender) ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            if (env.IsDevelopment())
            {
                logger.LogInformation("[FALLBACK REVIEW EMAIL] Review link for {Recipient}: {ReviewUrl}", toEmail, reviewUrl);
                return;
            }
            throw new InvalidOperationException("Chưa cấu hình SMTP để gửi lời mời đánh giá.");
        }

        using var client = new SmtpClient(server, smtp.GetValue<int>("Port", 587))
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password),
            Timeout = 15000
        };
        using var message = new MailMessage
        {
            From = new MailAddress(sender, smtp["SenderName"] ?? "Luminol Salon"),
            Subject = "Mời bạn đánh giá trải nghiệm tại Luminol Salon",
            IsBodyHtml = true,
            Body = $"<p>Chào {WebUtility.HtmlEncode(customerName)}, cảm ơn bạn đã trải nghiệm dịch vụ với {WebUtility.HtmlEncode(stylistName)}.</p><p><a href=\"{WebUtility.HtmlEncode(reviewUrl)}\">Gửi đánh giá của bạn</a></p><p>Liên kết có hiệu lực trong 7 ngày và chỉ dùng một lần.</p>"
        };
        message.To.Add(toEmail);
        await client.SendMailAsync(message);
    }

    private async Task<bool> LogStagingDelivery(string toEmail, string template, string? summary = null)
    {
        if (!env.IsStaging()) return false;
        logger.LogInformation("[STAGING EMAIL SIMULATION] {Template} to {Recipient}; external delivery suppressed.", template, toEmail);
        db.StagingEmailMessages.Add(new StagingEmailMessage
        {
            Template = template,
            Recipient = toEmail,
            Summary = summary ?? "Email được mô phỏng trên staging; không gửi tới hộp thư bên ngoài."
        });
        await db.SaveChangesAsync();
        return true;
    }

    private static string BuildRescheduleNotificationHtmlBody(
        string customerName,
        string stylistName,
        DateTime appointmentDate,
        TimeSpan startTime,
        TimeSpan endTime,
        string servicesSummary,
        string? reason,
        string salonAddress,
        string salonHotline) => $"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Cập nhật lịch hẹn</title>
</head>
<body style="margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #334155;">
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color: #f1f5f9; padding: 40px 15px;">
        <tr>
            <td align="center">
                <table role="presentation" width="100%" style="max-width: 600px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);">
                    <tr>
                        <td style="background: linear-gradient(135deg, #0284c7 0%, #0369a1 100%); padding: 32px 30px; text-align: center;">
                            <h1 style="margin: 0; font-size: 24px; font-weight: 700; color: #ffffff; letter-spacing: -0.5px;">
                                💈 Salon Management
                            </h1>
                            <p style="margin: 6px 0 0 0; font-size: 14px; color: #e0f2fe;">
                                Thông báo cập nhật lịch hẹn
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style="padding: 32px 30px;">
                            <p style="margin: 0 0 16px 0; font-size: 16px; font-weight: 600; color: #0f172a;">
                                Kính chào {WebUtility.HtmlEncode(customerName)},
                            </p>
                            <p style="margin: 0 0 20px 0; font-size: 14px; line-height: 1.6; color: #475569;">
                                Lịch hẹn của quý khách tại Salon đã được điều chỉnh thành công với thông tin mới nhất như sau:
                            </p>
                            <table style="width: 100%; border-collapse: collapse; background-color: #f8fafc; border-radius: 8px; border: 1px solid #e2e8f0; margin-bottom: 24px;">
                                <tr>
                                    <td style="padding: 12px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; width: 35%; color: #64748b; font-size: 13px;">Thợ phụ trách:</td>
                                    <td style="padding: 12px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #0f172a; font-size: 14px;">{WebUtility.HtmlEncode(stylistName)}</td>
                                </tr>
                                <tr>
                                    <td style="padding: 12px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #64748b; font-size: 13px;">Thời gian mới:</td>
                                    <td style="padding: 12px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #0284c7; font-size: 14px;">{appointmentDate:dd/MM/yyyy} ({startTime:hh\:mm} - {endTime:hh\:mm})</td>
                                </tr>
                                <tr>
                                    <td style="padding: 12px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #64748b; font-size: 13px;">Dịch vụ đã chọn:</td>
                                    <td style="padding: 12px 16px; border-bottom: 1px solid #e2e8f0; color: #334155; font-size: 13px;">{WebUtility.HtmlEncode(servicesSummary)}</td>
                                </tr>
                                {(string.IsNullOrWhiteSpace(reason) ? "" : $@"
                                <tr>
                                    <td style=""padding: 12px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #64748b; font-size: 13px;"">Ghi chú điều chỉnh:</td>
                                    <td style=""padding: 12px 16px; border-bottom: 1px solid #e2e8f0; color: #64748b; font-size: 13px; font-style: italic;"">{WebUtility.HtmlEncode(reason)}</td>
                                </tr>")}
                                <tr>
                                    <td style="padding: 12px 16px; border-bottom: 1px solid #e2e8f0; font-weight: 600; color: #64748b; font-size: 13px;">Địa chỉ salon:</td>
                                    <td style="padding: 12px 16px; border-bottom: 1px solid #e2e8f0; color: #334155; font-size: 13px;">{WebUtility.HtmlEncode(salonAddress)}</td>
                                </tr>
                                <tr>
                                    <td style="padding: 12px 16px; font-weight: 600; color: #64748b; font-size: 13px;">Hotline hỗ trợ:</td>
                                    <td style="padding: 12px 16px; font-weight: 600; color: #e11d48; font-size: 14px;">{WebUtility.HtmlEncode(salonHotline)}</td>
                                </tr>
                            </table>
                            <p style="margin: 0; font-size: 14px; line-height: 1.6; color: #475569;">
                                Nếu cần điều chỉnh thêm thông tin hoặc có thắc mắc, quý khách vui lòng liên hệ hotline trên để được phục vụ chu đáo nhất.
                            </p>
                        </td>
                    </tr>
                    <tr>
                        <td style="background-color: #f8fafc; padding: 20px 30px; text-align: center; border-top: 1px solid #e2e8f0;">
                            <p style="margin: 0; font-size: 12px; color: #94a3b8;">
                                © {DateTime.UtcNow.Year} Salon Management System. Hân hạnh được phục vụ quý khách.
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

