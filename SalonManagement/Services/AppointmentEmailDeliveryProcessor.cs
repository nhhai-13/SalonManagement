using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class AppointmentEmailDeliveryProcessor(
    ApplicationDbContext db,
    IEmailService emailService,
    TimeProvider clock,
    ILogger<AppointmentEmailDeliveryProcessor> logger)
{
    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var deliveries = await db.AppointmentEmailDeliveries
            .Where(item => (item.Status == AppointmentEmailStatuses.Queued || item.Status == AppointmentEmailStatuses.Retrying) && item.NextAttemptAt <= now)
            .OrderBy(item => item.NextAttemptAt).Take(50)
            .Include(item => item.Appointment).ThenInclude(item => item.Customer)
            .Include(item => item.Appointment).ThenInclude(item => item.Stylist)
            .Include(item => item.Appointment).ThenInclude(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .ToListAsync(cancellationToken);

        foreach (var delivery in deliveries)
        {
            var appointment = delivery.Appointment;
            if (delivery.Kind == AppointmentEmailKinds.Reminder)
            {
                if (appointment.Status != "Confirmed")
                {
                    delivery.Status = appointment.Status is "Cancelled" or "Canceled" or "Rejected" or "NoShow" or "Completed" or "Closed"
                        ? AppointmentEmailStatuses.Cancelled : AppointmentEmailStatuses.Queued;
                    delivery.NextAttemptAt = now.AddMinutes(10);
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var currentStart = CheckInService.StartsAt(appointment);
                if (delivery.AppointmentStartAt != currentStart)
                {
                    delivery.AppointmentStartAt = currentStart;
                    delivery.NextAttemptAt = currentStart.AddHours(-24);
                    if (delivery.NextAttemptAt < now) delivery.NextAttemptAt = now;
                    delivery.Status = AppointmentEmailStatuses.Queued;
                    if (delivery.NextAttemptAt > now)
                    {
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }
                }
                if (currentStart <= now)
                {
                    delivery.Status = AppointmentEmailStatuses.Cancelled;
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }
            }

            var email = delivery.RecipientEmail;
            if (string.IsNullOrWhiteSpace(email))
            {
                delivery.Status = AppointmentEmailStatuses.NoEmail;
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            delivery.Status = "Sending";
            delivery.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                var serviceNames = string.Join(", ", appointment.AppointmentServices.Select(item => item.Service.ServiceName));
                if (delivery.Kind == AppointmentEmailKinds.Reminder)
                {
                    await emailService.SendAppointmentReminderEmailAsync(email, appointment.Customer.FullName,
                        appointment.BookingReference, serviceNames, appointment.Stylist.FullName,
                        appointment.AppointmentDate, appointment.StartTime, appointment.EndTime);
                }
                else
                {
                    await emailService.SendBookingConfirmationEmailAsync(email, appointment.Customer.FullName,
                        appointment.BookingReference, serviceNames, appointment.Stylist.FullName,
                        appointment.AppointmentDate, appointment.StartTime, appointment.EndTime);
                }
                delivery.Status = AppointmentEmailStatuses.Sent;
                delivery.SentAt = clock.GetUtcNow();
                delivery.LastError = null;
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                delivery.LastError = exception.Message[..Math.Min(exception.Message.Length, 1000)];
                if (delivery.AttemptCount >= 3)
                {
                    delivery.Status = AppointmentEmailStatuses.Failed;
                }
                else
                {
                    delivery.Status = AppointmentEmailStatuses.Retrying;
                    delivery.NextAttemptAt = clock.GetUtcNow().AddMinutes(10);
                }
                await db.SaveChangesAsync(cancellationToken);
                logger.LogWarning(exception, "Appointment email delivery {DeliveryId} failed on attempt {Attempt}.",
                    delivery.AppointmentEmailDeliveryId, delivery.AttemptCount);
            }
        }
    }
}

public sealed class AppointmentEmailDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AppointmentEmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppointmentEmailDeliveryProcessor>().ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Không xử lý được hàng đợi email lịch hẹn."); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
