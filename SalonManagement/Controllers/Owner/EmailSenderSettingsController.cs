using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
public sealed class EmailSenderSettingsPageController : Controller
{
    [HttpGet("owner/email-settings")]
    public IActionResult Index() => View("~/Views/EmailSenderSettings/Index.cshtml");
}

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/owner/email-settings")]
public sealed class EmailSenderSettingsController(
    IEmailSenderSettingsStore settingsStore,
    IEmailService emailService,
    ILogger<EmailSenderSettingsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SmtpSenderSettingsStatus>> Get(CancellationToken cancellationToken) =>
        Ok(await settingsStore.GetStatusAsync(cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Save([FromBody] UpdateEmailSenderSettingsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await settingsStore.SaveAsync(new SmtpSenderSettings(
                request.Server ?? string.Empty, request.Port, request.SenderEmail ?? string.Empty,
                request.Username ?? string.Empty, request.AppPassword ?? string.Empty), cancellationToken);
            return Ok(new { message = "Đã lưu cấu hình email gửi. Bạn có thể gửi thư thử ngay." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("send-test")]
    public async Task<IActionResult> SendTest([FromBody] SendTestEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            return BadRequest(new { error = "Vui lòng nhập email nhận thư thử." });
        }

        try
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            await emailService.SendEmailVerificationCodeAsync(request.RecipientEmail.Trim(), code);
            return Ok(new { message = "Đã gửi thư thử. Hãy kiểm tra Hộp thư đến hoặc Spam/Rác." });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gửi thư thử thất bại tới {Email}", request.RecipientEmail);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Không thể gửi email. Kiểm tra lại email gửi và App Password." });
        }
    }
}

public sealed record UpdateEmailSenderSettingsRequest(string? Server, int Port, string? SenderEmail, string? Username, string? AppPassword);
public sealed record SendTestEmailRequest(string? RecipientEmail);
