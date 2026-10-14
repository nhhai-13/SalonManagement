using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SalonManagement.Services.Appointments;

/// <summary>
/// Background worker tiêu thụ hàng đợi email thông báo dời lịch (AC5).
/// Thực hiện retry tối đa 3 lần nếu xảy ra lỗi SMTP / network.
/// </summary>
public class AppointmentRescheduleEmailWorker : BackgroundService
{
    private readonly IAppointmentRescheduleEmailQueue _queue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AppointmentRescheduleEmailWorker> _logger;

    public AppointmentRescheduleEmailWorker(
        IAppointmentRescheduleEmailQueue queue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AppointmentRescheduleEmailWorker> logger)
    {
        _queue = queue;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentRescheduleEmailWorker đã khởi động.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await _queue.DequeueAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(message.CustomerEmail))
                {
                    continue; // Bỏ qua an toàn nếu không có email
                }

                await ProcessWithRetryAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi bất thường trong background worker gửi email dời lịch");
            }
        }

        _logger.LogInformation("AppointmentRescheduleEmailWorker đang dừng.");
    }

    public async Task<bool> ProcessWithRetryAsync(RescheduleEmailMessage message, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                await emailService.SendRescheduleNotificationEmailAsync(
                    message.CustomerEmail,
                    message.CustomerName,
                    message.StylistName,
                    message.AppointmentDate,
                    message.StartTime,
                    message.EndTime,
                    message.ServicesSummary,
                    message.Reason,
                    message.SalonAddress,
                    message.SalonHotline);

                _logger.LogInformation("Gửi email thông báo dời lịch thành công cho lịch #{AppointmentId} tới {Email}",
                    message.AppointmentId, message.CustomerEmail);
                return true;
            }
            catch (Exception ex)
            {
                message.RetryCount = attempt;
                _logger.LogWarning(ex, "Gửi email dời lịch #{AppointmentId} tới {Email} lần {Attempt}/{MaxRetries} thất bại",
                    message.AppointmentId, message.CustomerEmail, attempt, maxRetries);

                if (attempt >= maxRetries)
                {
                    _logger.LogError(ex, "Đã thử {MaxRetries} lần nhưng không thể gửi email dời lịch cho lịch #{AppointmentId} tới {Email}",
                        maxRetries, message.AppointmentId, message.CustomerEmail);
                    return false;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1)), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        return false;
    }
}
