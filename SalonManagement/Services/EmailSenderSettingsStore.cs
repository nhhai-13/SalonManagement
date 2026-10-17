using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace SalonManagement.Services;

/// <summary>Thông tin SMTP dùng để gửi email. Mật khẩu luôn được bảo vệ trước khi ghi ra ổ đĩa.</summary>
public sealed record SmtpSenderSettings(string Server, int Port, string SenderEmail, string Username, string AppPassword);
public sealed record SmtpSenderSettingsStatus(bool IsConfigured, string? Server, int? Port, string? SenderEmail, string? Username, DateTimeOffset? UpdatedAt);

public interface IEmailSenderSettingsStore
{
    Task<SmtpSenderSettings?> GetAsync(CancellationToken cancellationToken = default);
    Task<SmtpSenderSettingsStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(SmtpSenderSettings settings, CancellationToken cancellationToken = default);
}

public sealed class EmailSenderSettingsStore : IEmailSenderSettingsStore
{
    private readonly string settingsPath;
    private readonly IDataProtector protector;
    private readonly SemaphoreSlim gate = new(1, 1);

    public EmailSenderSettingsStore(IWebHostEnvironment environment, IDataProtectionProvider dataProtectionProvider)
    {
        settingsPath = Path.Combine(environment.ContentRootPath, "App_Data", "email-sender-settings.protected.json");
        protector = dataProtectionProvider.CreateProtector("LuminolSalon.EmailSenderSettings.v1");
    }

    public async Task<SmtpSenderSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        var payload = await ReadAsync(cancellationToken);
        if (payload is null) return null;

        try
        {
            return new SmtpSenderSettings(
                payload.Server,
                payload.Port,
                payload.SenderEmail,
                payload.Username,
                protector.Unprotect(payload.ProtectedAppPassword));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public async Task<SmtpSenderSettingsStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var payload = await ReadAsync(cancellationToken);
        return payload is null
            ? new SmtpSenderSettingsStatus(false, null, null, null, null, null)
            : new SmtpSenderSettingsStatus(true, payload.Server, payload.Port, payload.SenderEmail, payload.Username, payload.UpdatedAt);
    }

    public async Task SaveAsync(SmtpSenderSettings settings, CancellationToken cancellationToken = default)
    {
        Validate(settings);
        var payload = new StoredEmailSenderSettings(
            settings.Server.Trim(), settings.Port, settings.SenderEmail.Trim(), settings.Username.Trim(),
            protector.Protect(settings.AppPassword.Trim()), DateTimeOffset.UtcNow);
        var json = JsonSerializer.Serialize(payload);
        var directory = Path.GetDirectoryName(settingsPath)!;
        Directory.CreateDirectory(directory);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var temporaryPath = settingsPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, settingsPath, true);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<StoredEmailSenderSettings?> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(settingsPath)) return null;
        await gate.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(settingsPath, cancellationToken);
            return JsonSerializer.Deserialize<StoredEmailSenderSettings>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private static void Validate(SmtpSenderSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Server) || settings.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(settings.SenderEmail) || string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.AppPassword))
        {
            throw new ArgumentException("Vui lòng nhập đầy đủ thông tin SMTP.");
        }

        _ = new System.Net.Mail.MailAddress(settings.SenderEmail);
    }

    private sealed record StoredEmailSenderSettings(
        string Server, int Port, string SenderEmail, string Username,
        string ProtectedAppPassword, DateTimeOffset UpdatedAt);
}
