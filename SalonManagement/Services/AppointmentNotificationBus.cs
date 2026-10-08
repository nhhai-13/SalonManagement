using System.Collections.Concurrent;
using System.Threading.Channels;
using SalonManagement.Models;

namespace SalonManagement.Services;
// Live delivery is per stylist; notifications are also durable in the database for reconnects.
public sealed class AppointmentNotificationBus
{
    private readonly ConcurrentDictionary<Guid, (int StylistId, Channel<StylistNotification> Channel)> subscribers = new();
    public (Guid Id, ChannelReader<StylistNotification> Reader) Subscribe(int stylistId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<StylistNotification>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });
        subscribers[id] = (stylistId, channel);
        return (id, channel.Reader);
    }
    public void Unsubscribe(Guid id)
    {
        if (subscribers.TryRemove(id, out var subscription)) subscription.Channel.Writer.TryComplete();
    }
    public void Publish(StylistNotification notification)
    {
        foreach (var subscription in subscribers.Values)
            if (subscription.StylistId == notification.StylistId) subscription.Channel.Writer.TryWrite(notification);
    }
}
