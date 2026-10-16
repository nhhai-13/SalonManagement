namespace SalonManagement.Services;

public sealed class AppointmentEmailNotificationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AppointmentEmailNotificationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppointmentEmailNotificationProcessor>().ProcessPendingAsync(stoppingToken);
            }
            catch (Exception exception) { logger.LogError(exception, "Không thể xử lý hàng đợi email xác nhận và nhắc lịch."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
