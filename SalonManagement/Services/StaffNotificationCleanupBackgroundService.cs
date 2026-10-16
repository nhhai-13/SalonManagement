namespace SalonManagement.Services;

/// <summary>Removes old in-app inbox entries once a day (S4-06 AC4).</summary>
public sealed class StaffNotificationCleanupBackgroundService(IServiceScopeFactory scopeFactory, ILogger<StaffNotificationCleanupBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var removed = await scope.ServiceProvider.GetRequiredService<StylistInAppNotificationService>()
                    .DeleteOlderThanThirtyDaysAsync(stoppingToken);
                if (removed > 0) logger.LogInformation("Removed {Count} staff notifications older than 30 days.", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not clean up old staff notifications.");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
