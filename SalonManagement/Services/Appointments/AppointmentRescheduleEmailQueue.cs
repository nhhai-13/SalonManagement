using System.Threading.Channels;

namespace SalonManagement.Services.Appointments;

public class RescheduleEmailMessage
{
    public int AppointmentId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string ServicesSummary { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string SalonAddress { get; set; } = "123 Đường Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh";
    public string SalonHotline { get; set; } = "1900 1234";
    public int RetryCount { get; set; } = 0;
}

public interface IAppointmentRescheduleEmailQueue
{
    ValueTask EnqueueAsync(RescheduleEmailMessage message, CancellationToken cancellationToken = default);
    ValueTask<RescheduleEmailMessage> DequeueAsync(CancellationToken cancellationToken);
}

public class AppointmentRescheduleEmailQueue : IAppointmentRescheduleEmailQueue
{
    private readonly Channel<RescheduleEmailMessage> _queue;

    public AppointmentRescheduleEmailQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<RescheduleEmailMessage>(options);
    }

    public async ValueTask EnqueueAsync(RescheduleEmailMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(message.CustomerEmail)) return; // Bỏ qua an toàn nếu khách không có email

        await _queue.Writer.WriteAsync(message, cancellationToken);
    }

    public async ValueTask<RescheduleEmailMessage> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
