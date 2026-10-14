using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;

namespace SalonManagement.Services;

public sealed class AppointmentChangeEmailProcessor(ApplicationDbContext db, IEmailService emailService, TimeProvider timeProvider)
{
    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = SalonClock.GetLocalNow(timeProvider);
        var messages = await db.AppointmentChangeEmails
            .Where(message => (message.Status == "Queued" || message.Status == "Retrying") && message.NextAttemptAt <= now)
            .OrderBy(message => message.NextAttemptAt).Take(20).ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            message.Status = "Sending";
            message.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                await emailService.SendAppointmentChangeEmailAsync(message.RecipientEmail, message.StylistName, message.AppointmentDate, message.StartTime);
                message.Status = "Sent";
                message.SentAt = SalonClock.GetLocalNow(timeProvider);
                message.LastError = null;
                await UpdateHistoryStatusAsync(message.AppointmentChangeLogId, "Sent", cancellationToken);
            }
            catch (Exception exception)
            {
                message.LastError = exception.Message[..Math.Min(exception.Message.Length, 1000)];
                if (message.AttemptCount >= 3)
                {
                    message.Status = "Failed";
                    await UpdateHistoryStatusAsync(message.AppointmentChangeLogId, "Failed", cancellationToken);
                }
                else
                {
                    message.Status = "Retrying";
                    message.NextAttemptAt = SalonClock.GetLocalNow(timeProvider).AddMinutes(message.AttemptCount);
                    await UpdateHistoryStatusAsync(message.AppointmentChangeLogId, "Retrying", cancellationToken);
                }
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task UpdateHistoryStatusAsync(int logId, string status, CancellationToken cancellationToken)
    {
        var log = await db.AppointmentChangeLogs.SingleAsync(item => item.AppointmentChangeLogId == logId, cancellationToken);
        log.EmailStatus = status;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AppointmentChangeEmailBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AppointmentChangeEmailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppointmentChangeEmailProcessor>().ProcessPendingAsync(stoppingToken);
            }
            catch (Exception exception) { logger.LogError(exception, "Không thể xử lý hàng đợi email đổi lịch."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
