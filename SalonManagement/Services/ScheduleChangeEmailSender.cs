using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SalonManagement.Services;

public sealed class ScheduleEmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "SalonManagement";
    public bool EnableSsl { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 10;
    public string SupportPhone { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
}

public sealed record ScheduleChangeEmail(
    string RecipientEmail,
    string CustomerName,
    string ServiceNames,
    DateTime AppointmentDate,
    string OldStylistName,
    string NewStylistName,
    TimeSpan OldStartTime,
    TimeSpan NewStartTime);

public interface IScheduleChangeEmailSender
{
    Task SendAsync(ScheduleChangeEmail email, CancellationToken cancellationToken);
}

public sealed class SmtpScheduleChangeEmailSender(IOptions<ScheduleEmailOptions> options)
    : IScheduleChangeEmailSender
{
    public async Task SendAsync(ScheduleChangeEmail email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) ||
            string.IsNullOrWhiteSpace(settings.FromAddress) ||
            settings.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Chưa cấu hình máy chủ SMTP cho email thông báo lịch hẹn.");
        }

        var subject = CreateSubject(email);
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = subject,
            Body = CreateHtmlBody(email, settings),
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email.RecipientEmail));

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = Math.Clamp(settings.TimeoutSeconds, 1, 30) * 1000
        };
        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    public static string CreateSubject(ScheduleChangeEmail email)
    {
        var stylistChanged = !string.Equals(email.OldStylistName, email.NewStylistName, StringComparison.Ordinal);
        var timeChanged = email.OldStartTime != email.NewStartTime;
        var change = (stylistChanged, timeChanged) switch
        {
            (true, true) => "Cập nhật thợ và giờ hẹn",
            (true, false) => "Cập nhật thợ phụ trách lịch hẹn",
            (false, true) => "Cập nhật giờ hẹn",
            _ => "Thông tin lịch hẹn đã được cập nhật"
        };
        return $"{change} - SalonManagement";
    }

    public static string CreateHtmlBody(ScheduleChangeEmail email, ScheduleEmailOptions settings)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        var date = email.AppointmentDate.ToString("dddd, dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
        var support = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.SupportPhone))
        {
            support.Add($"điện thoại <a href=\"tel:{E(settings.SupportPhone)}\">{E(settings.SupportPhone)}</a>");
        }
        if (!string.IsNullOrWhiteSpace(settings.SupportEmail))
        {
            support.Add($"email <a href=\"mailto:{E(settings.SupportEmail)}\">{E(settings.SupportEmail)}</a>");
        }
        var supportText = support.Count == 0
            ? "vui lòng liên hệ salon để được hỗ trợ."
            : $"vui lòng liên hệ salon qua {string.Join(" hoặc ", support)} nếu cần hỗ trợ.";

        return $"""
            <div style="font-family:Arial,sans-serif;color:#252525;line-height:1.6;max-width:620px;margin:auto">
              <h2>Thông tin lịch hẹn của bạn</h2>
              <p>Xin chào {E(email.CustomerName)}, salon xin thông báo lịch hẹn của bạn đã được điều chỉnh.</p>
              <p><strong>Dịch vụ:</strong> {E(email.ServiceNames)}<br />
                 <strong>Ngày hẹn:</strong> {E(date)}</p>
              <table style="border-collapse:collapse;width:100%">
                <thead><tr><th style="text-align:left;padding:8px;border-bottom:1px solid #ddd">Thông tin</th>
                    <th style="text-align:left;padding:8px;border-bottom:1px solid #ddd">Trước khi điều chỉnh</th>
                    <th style="text-align:left;padding:8px;border-bottom:1px solid #ddd">Thông tin mới</th></tr></thead>
                <tbody>
                  <tr><td style="padding:8px;border-bottom:1px solid #eee">Thợ phụ trách</td><td style="padding:8px;border-bottom:1px solid #eee">{E(email.OldStylistName)}</td><td style="padding:8px;border-bottom:1px solid #eee">{E(email.NewStylistName)}</td></tr>
                  <tr><td style="padding:8px;border-bottom:1px solid #eee">Giờ hẹn</td><td style="padding:8px;border-bottom:1px solid #eee">{email.OldStartTime:hh\:mm}</td><td style="padding:8px;border-bottom:1px solid #eee">{email.NewStartTime:hh\:mm}</td></tr>
                </tbody>
              </table>
              <p>Nếu cần hỗ trợ, {supportText}</p>
              <p>Trân trọng,<br />{E(settings.FromName)}</p>
            </div>
            """;
    }
}
