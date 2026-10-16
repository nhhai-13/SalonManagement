using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class BatchAppointmentReschedulingTests
{
    [TestMethod]
    public async Task Batch_moves_multiple_valid_appointments_and_queues_email_notifications()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);

        var result = await Batch(db).RescheduleAsync(
            [new(seed.FirstAppointmentId, seed.ReplacementStylistId, TimeSpan.FromHours(11)), new(seed.SecondAppointmentId, seed.ReplacementStylistId, TimeSpan.FromHours(12))],
            new AppointmentChangeActor("reception-1", "Lễ tân"));

        Assert.AreEqual(2, result.Succeeded.Count);
        Assert.AreEqual(0, result.Failed.Count);
        var moved = await db.Appointments.Where(item => item.StylistId == seed.ReplacementStylistId).OrderBy(item => item.StartTime).ToListAsync();
        CollectionAssert.AreEqual(new[] { TimeSpan.FromHours(11), TimeSpan.FromHours(12) }, moved.Select(item => item.StartTime).ToArray());
        CollectionAssert.AreEqual(new[] { "Queued", "Queued" }, result.Succeeded.Select(item => item.NotificationStatus).ToArray());
        Assert.AreEqual(2, await db.AppointmentChangeEmails.CountAsync());
        var sender = new RecordingEmailService();
        await new AppointmentChangeEmailProcessor(db, sender, TimeProvider.System).ProcessPendingAsync();
        var notifications = await db.AppointmentChangeEmails.OrderBy(item => item.RecipientEmail).ToListAsync();
        Assert.IsTrue(notifications.All(item => item.Status == "Sent"));
        Assert.IsTrue(notifications.All(item => item.SentAt!.Value - item.NextAttemptAt <= TimeSpan.FromMinutes(5)));
        CollectionAssert.AreEquivalent(new[] { "move01@example.test", "move02@example.test" }, sender.Recipients);
    }

    [TestMethod]
    public async Task Batch_keeps_successful_moves_when_another_item_conflicts()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        db.Appointments.Add(new Appointment
        {
            Customer = new Customer { FullName = "Khách trùng", Phone = "0900000008" },
            StylistId = seed.ReplacementStylistId,
            AppointmentDate = seed.Date,
            StartTime = TimeSpan.FromHours(13),
            EndTime = TimeSpan.FromHours(13.5),
            Status = "Confirmed",
            BookingReference = "BLOCK13",
            AppointmentServices = [new AppointmentService { ServiceId = seed.ServiceId, Price = 100000, DurationMinutes = 30 }]
        });
        await db.SaveChangesAsync();

        var result = await Batch(db).RescheduleAsync(
            [new(seed.FirstAppointmentId, seed.ReplacementStylistId, TimeSpan.FromHours(11)), new(seed.SecondAppointmentId, seed.ReplacementStylistId, TimeSpan.FromHours(13))],
            new AppointmentChangeActor("reception-1", "Lễ tân"));

        Assert.AreEqual(1, result.Succeeded.Count);
        Assert.AreEqual(seed.FirstAppointmentId, result.Succeeded[0].AppointmentId);
        Assert.AreEqual(1, result.Failed.Count);
        Assert.AreEqual(seed.SecondAppointmentId, result.Failed[0].AppointmentId);
        Assert.AreEqual("overlap", result.Failed[0].ErrorCode);
        var first = await db.Appointments.FindAsync(seed.FirstAppointmentId);
        var second = await db.Appointments.FindAsync(seed.SecondAppointmentId);
        Assert.AreEqual(seed.ReplacementStylistId, first!.StylistId);
        Assert.AreEqual(TimeSpan.FromHours(11), first.StartTime);
        Assert.AreEqual(seed.OriginalStylistId, second!.StylistId);
        Assert.AreEqual(TimeSpan.FromHours(10), second.StartTime);
    }

    [TestMethod]
    public async Task Batch_marks_missing_email_as_not_required_without_blocking_other_notifications()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var secondAppointment = await db.Appointments.Include(item => item.Customer).SingleAsync(item => item.AppointmentId == seed.SecondAppointmentId);
        secondAppointment.Customer.Email = null;
        await db.SaveChangesAsync();

        var result = await Batch(db).RescheduleAsync(
            [new(seed.FirstAppointmentId, seed.ReplacementStylistId, TimeSpan.FromHours(11)), new(seed.SecondAppointmentId, seed.ReplacementStylistId, TimeSpan.FromHours(12))],
            new AppointmentChangeActor("reception-1", "Lễ tân"));

        CollectionAssert.AreEquivalent(new[] { "Queued", "NotRequired" }, result.Succeeded.Select(item => item.NotificationStatus).ToArray());
        Assert.AreEqual(1, await db.AppointmentChangeEmails.CountAsync());
        var logs = await db.AppointmentChangeLogs.OrderBy(item => item.AppointmentId).ToListAsync();
        CollectionAssert.AreEquivalent(new[] { "Queued", "NotRequired" }, logs.Select(item => item.EmailStatus).ToArray());
    }

    [TestMethod]
    public async Task Failed_email_does_not_undo_a_successful_batch_reschedule()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var result = await Batch(db).RescheduleAsync(
            [new(seed.FirstAppointmentId, seed.ReplacementStylistId, TimeSpan.FromHours(11))],
            new AppointmentChangeActor("reception-1", "Lễ tân"));
        var notification = await db.AppointmentChangeEmails.SingleAsync();
        notification.AttemptCount = 2;
        await db.SaveChangesAsync();

        await new AppointmentChangeEmailProcessor(db, new RecordingEmailService(throwOnSend: true), TimeProvider.System).ProcessPendingAsync();

        Assert.AreEqual("Queued", result.Succeeded.Single().NotificationStatus);
        Assert.AreEqual(seed.ReplacementStylistId, (await db.Appointments.FindAsync(seed.FirstAppointmentId))!.StylistId);
        Assert.AreEqual("Failed", (await db.AppointmentChangeEmails.SingleAsync()).Status);
        Assert.AreEqual("Failed", (await db.AppointmentChangeLogs.SingleAsync()).EmailStatus);
    }

    private static BatchAppointmentReschedulingService Batch(ApplicationDbContext db)
        => new(new AppointmentReschedulingService(db, TimeProvider.System));

    private static async Task<(int FirstAppointmentId, int SecondAppointmentId, int OriginalStylistId, int ReplacementStylistId, int ServiceId, DateTime Date)> SeedAsync(ApplicationDbContext db)
    {
        var date = new DateTime(2031, 2, 3);
        var service = new Service { ServiceName = "Cắt kiểm thử", DurationMinutes = 30, Price = 100000, IsActive = true };
        var original = new Stylist { FullName = "Thợ nghỉ", Phone = "0900000001", IsActive = true };
        var replacement = new Stylist { FullName = "Thợ thay thế", Phone = "0900000002", IsActive = true };
        db.AddRange(service, original, replacement);
        await db.SaveChangesAsync();
        db.StylistServices.AddRange(
            new StylistService { StylistId = original.StylistId, ServiceId = service.ServiceId },
            new StylistService { StylistId = replacement.StylistId, ServiceId = service.ServiceId });
        db.WorkSchedules.Add(new WorkSchedule { StylistId = replacement.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" });
        var first = Appointment("MOVE01", "Khách một", "0900000003", original.StylistId, service.ServiceId, date, 9);
        var second = Appointment("MOVE02", "Khách hai", "0900000004", original.StylistId, service.ServiceId, date, 10);
        db.Appointments.AddRange(first, second);
        await db.SaveChangesAsync();
        return (first.AppointmentId, second.AppointmentId, original.StylistId, replacement.StylistId, service.ServiceId, date);
    }

    private static Appointment Appointment(string reference, string name, string phone, int stylistId, int serviceId, DateTime date, int hour) => new()
    {
        Customer = new Customer { FullName = name, Phone = phone, Email = $"{reference.ToLowerInvariant()}@example.test" },
        StylistId = stylistId,
        AppointmentDate = date,
        StartTime = TimeSpan.FromHours(hour),
        EndTime = TimeSpan.FromHours(hour + .5),
        Status = "Confirmed",
        BookingReference = reference,
        AppointmentServices = [new AppointmentService { ServiceId = serviceId, Price = 100000, DurationMinutes = 30 }]
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private sealed class RecordingEmailService(bool throwOnSend = false) : IEmailService
    {
        public List<string> Recipients { get; } = [];
        public Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword) => Task.CompletedTask;
        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink) => Task.CompletedTask;
        public Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode) => Task.CompletedTask;
        public Task SendServiceReviewEmailAsync(string toEmail, string customerName, string reviewLink, DateTime expiresAt) => Task.CompletedTask;
        public Task SendAppointmentNotificationEmailAsync(string toEmail, string customerName, string reference, string services, string stylistName, DateTime appointmentDate, TimeSpan startTime, bool isReminder) => Task.CompletedTask;
        public Task SendAppointmentChangeEmailAsync(string toEmail, string stylistName, DateTime appointmentDate, TimeSpan startTime)
        {
            if (throwOnSend) throw new InvalidOperationException("Máy chủ email tạm thời không phản hồi.");
            Recipients.Add(toEmail);
            return Task.CompletedTask;
        }
    }
}
