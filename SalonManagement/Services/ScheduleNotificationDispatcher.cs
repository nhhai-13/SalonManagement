using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class ScheduleNotificationOptions
{
    public int PollIntervalSeconds { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int LeaseTimeoutSeconds { get; set; } = 30;
    public int[] RetryDelaySeconds { get; set; } = [15, 30, 60, 120];
}

public sealed class ScheduleNotificationDispatcher(
    ApplicationDbContext dbContext,
    IScheduleChangeEmailSender emailSender,
    IOptions<ScheduleEmailOptions> emailOptions,
    IOptions<ScheduleNotificationOptions> notificationOptions,
    TimeProvider timeProvider,
    ILogger<ScheduleNotificationDispatcher> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetLocalNow().DateTime;
        var policy = notificationOptions.Value;
        var staleSendingBefore = now.AddSeconds(-Math.Max(10, policy.LeaseTimeoutSeconds));
        var staleFinal = 0;
        if (dbContext.Database.IsRelational())
        {
            staleFinal = await dbContext.ScheduleChangeNotifications
                .Where(item => item.Status == ScheduleChangeNotificationStatuses.Sending &&
                    item.AttemptCount >= policy.MaxAttempts && item.LastAttemptAt <= staleSendingBefore)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, ScheduleChangeNotificationStatuses.Failed)
                    .SetProperty(item => item.LastError, "Worker dừng giữa lần gửi cuối; cần kiểm tra cấu hình SMTP."),
                    cancellationToken);
        }
        else
        {
            var staleFinalItems = await dbContext.ScheduleChangeNotifications
                .Where(item => item.Status == ScheduleChangeNotificationStatuses.Sending &&
                    item.AttemptCount >= policy.MaxAttempts && item.LastAttemptAt <= staleSendingBefore)
                .ToListAsync(cancellationToken);
            foreach (var item in staleFinalItems)
            {
                item.Status = ScheduleChangeNotificationStatuses.Failed;
                item.LastError = "Worker dừng giữa lần gửi cuối; cần kiểm tra cấu hình SMTP.";
            }
            staleFinal = staleFinalItems.Count;
            if (staleFinal > 0) await dbContext.SaveChangesAsync(cancellationToken);
        }

        var candidate = await dbContext.ScheduleChangeNotifications.AsNoTracking()
            .Where(item =>
                (item.Status == ScheduleChangeNotificationStatuses.Pending &&
                    (item.NextAttemptAt == null || item.NextAttemptAt <= now)) ||
                (item.Status == ScheduleChangeNotificationStatuses.Sending &&
                    item.AttemptCount < policy.MaxAttempts && item.LastAttemptAt <= staleSendingBefore))
            .OrderBy(item => item.NextAttemptAt)
            .ThenBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return staleFinal > 0;
        }

        var priorStatus = candidate.Status;
        var attemptNumber = candidate.AttemptCount + 1;
        if (dbContext.Database.IsRelational())
        {
            var claimed = await dbContext.ScheduleChangeNotifications
                .Where(item => item.ScheduleChangeNotificationId == candidate.ScheduleChangeNotificationId &&
                    item.Status == priorStatus &&
                    (priorStatus == ScheduleChangeNotificationStatuses.Pending
                        ? item.NextAttemptAt == null || item.NextAttemptAt <= now
                        : item.LastAttemptAt <= staleSendingBefore))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, ScheduleChangeNotificationStatuses.Sending)
                    .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                    .SetProperty(item => item.LastAttemptAt, now), cancellationToken);
            if (claimed == 0)
            {
                return true;
            }
        }
        else
        {
            candidate = await dbContext.ScheduleChangeNotifications
                .SingleAsync(item => item.ScheduleChangeNotificationId == candidate.ScheduleChangeNotificationId,
                    cancellationToken);
            candidate.Status = ScheduleChangeNotificationStatuses.Sending;
            candidate.AttemptCount = attemptNumber;
            candidate.LastAttemptAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var email = new ScheduleChangeEmail(
            candidate.RecipientEmail,
            candidate.CustomerName,
            candidate.ServiceNames,
            candidate.AppointmentDate,
            candidate.OldStylistName,
            candidate.NewStylistName,
            candidate.OldStartTime,
            candidate.NewStartTime);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(emailOptions.Value.TimeoutSeconds, 1, 30)));
            await emailSender.SendAsync(email, timeout.Token);
            var sent = await dbContext.ScheduleChangeNotifications
                .SingleAsync(item => item.ScheduleChangeNotificationId == candidate.ScheduleChangeNotificationId,
                    cancellationToken);
            sent.Status = ScheduleChangeNotificationStatuses.Sent;
            sent.SentAt = timeProvider.GetLocalNow().DateTime;
            sent.NextAttemptAt = null;
            sent.LastError = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failed = await dbContext.ScheduleChangeNotifications
                .SingleAsync(item => item.ScheduleChangeNotificationId == candidate.ScheduleChangeNotificationId,
                    cancellationToken);
            var failedAt = timeProvider.GetLocalNow().DateTime;
            failed.LastError = exception.Message.Length <= 1000 ? exception.Message : exception.Message[..1000];
            failed.Status = attemptNumber >= policy.MaxAttempts
                ? ScheduleChangeNotificationStatuses.Failed
                : ScheduleChangeNotificationStatuses.Pending;
            failed.NextAttemptAt = failed.Status == ScheduleChangeNotificationStatuses.Pending
                ? failedAt.AddSeconds(GetRetryDelaySeconds(policy, attemptNumber))
                : null;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(exception,
                "Schedule change email {NotificationId} failed on attempt {Attempt}/{MaxAttempts}",
                candidate.ScheduleChangeNotificationId, attemptNumber, policy.MaxAttempts);
        }

        return true;
    }

    private static int GetRetryDelaySeconds(ScheduleNotificationOptions options, int attemptNumber)
    {
        var retryIndex = Math.Clamp(attemptNumber - 1, 0, Math.Max(0, options.RetryDelaySeconds.Length - 1));
        return options.RetryDelaySeconds.Length == 0
            ? 30
            : Math.Clamp(options.RetryDelaySeconds[retryIndex], 1, 240);
    }
}

public sealed class ScheduleNotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ScheduleNotificationOptions> options,
    ILogger<ScheduleNotificationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollIntervalSeconds, 1, 30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<ScheduleNotificationDispatcher>();
                var hadWork = await dispatcher.ProcessNextAsync(stoppingToken);
                if (hadWork)
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Schedule change notification worker iteration failed");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
