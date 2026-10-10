using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class AppointmentReschedulingServiceTests
{
    [TestMethod]
    public async Task Reschedule_valid_change_updates_stylist_times_and_keeps_duration()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        var result = await new AppointmentReschedulingService(db, TimeProvider.System)
            .RescheduleAsync(fixture.AppointmentId, new RescheduleAppointmentRequest(fixture.AlternateStylistId, TimeSpan.FromHours(11)), new AppointmentChangeActor("reception-1", "Lễ tân"));

        Assert.IsTrue(result.Succeeded);
        var saved = await db.Appointments.SingleAsync();
        Assert.AreEqual(fixture.AlternateStylistId, saved.StylistId);
        Assert.AreEqual(TimeSpan.FromHours(11), saved.StartTime);
        Assert.AreEqual(TimeSpan.FromHours(11.5), saved.EndTime);
        var log = await db.AppointmentChangeLogs.SingleAsync();
        Assert.AreEqual("reception-1", log.ActorId);
        Assert.AreEqual(fixture.OriginalStylistId, log.OldStylistId);
        Assert.AreEqual(fixture.AlternateStylistId, log.NewStylistId);
        Assert.AreEqual(TimeSpan.FromHours(9), log.OldStartTime);
        Assert.AreEqual(TimeSpan.FromHours(11), log.NewStartTime);
        var email = await db.AppointmentChangeEmails.SingleAsync();
        Assert.AreEqual("Queued", email.Status);
        var sender = new FakeEmailService();
        await new AppointmentChangeEmailProcessor(db, sender, TimeProvider.System).ProcessPendingAsync();
        Assert.AreEqual("Sent", (await db.AppointmentChangeEmails.SingleAsync()).Status);
        Assert.AreEqual("demo@example.test", sender.Recipient);
        Assert.AreEqual("Thợ mới", sender.StylistName);
        Assert.AreEqual("Sent", (await db.AppointmentChangeLogs.SingleAsync()).EmailStatus);
    }

    [TestMethod]
    public async Task Reschedule_stylist_missing_skill_is_rejected_with_f33_without_changes()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        var result = await new AppointmentReschedulingService(db, TimeProvider.System)
            .RescheduleAsync(fixture.AppointmentId, new RescheduleAppointmentRequest(fixture.UnqualifiedStylistId, TimeSpan.FromHours(11)));

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("F33", result.ErrorCode);
        StringAssert.Contains(result.Error!, "Cắt");
        var saved = await db.Appointments.SingleAsync();
        Assert.AreEqual(fixture.OriginalStylistId, saved.StylistId);
        Assert.AreEqual(TimeSpan.FromHours(9), saved.StartTime);
        Assert.AreEqual(0, await db.AppointmentChangeLogs.CountAsync());
    }

    [TestMethod]
    public async Task Reschedule_overlap_or_outside_shift_is_rejected_with_specific_reason()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);
        db.Appointments.Add(new Appointment { Customer = new Customer { FullName = "Khách đang có lịch", Phone = "0900000010" }, StylistId = fixture.AlternateStylistId, AppointmentDate = fixture.Date, StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(12), Status = "Confirmed", BookingReference = "OTHER001" });
        await db.SaveChangesAsync();
        var service = new AppointmentReschedulingService(db, TimeProvider.System);

        var overlap = await service.RescheduleAsync(fixture.AppointmentId, new RescheduleAppointmentRequest(fixture.AlternateStylistId, TimeSpan.FromHours(11.5)));
        var outsideShift = await service.RescheduleAsync(fixture.AppointmentId, new RescheduleAppointmentRequest(fixture.AlternateStylistId, TimeSpan.FromHours(17)));

        Assert.AreEqual("overlap", overlap.ErrorCode);
        StringAssert.Contains(overlap.Error!, "Khách đang có lịch");
        Assert.AreEqual("outside_shift", outsideShift.ErrorCode);
        var saved = await db.Appointments.SingleAsync(item => item.AppointmentId == fixture.AppointmentId);
        Assert.AreEqual(TimeSpan.FromHours(9), saved.StartTime);
        Assert.AreEqual(0, await db.AppointmentChangeLogs.CountAsync());
    }

    [TestMethod]
    public async Task Reschedule_without_changes_does_not_create_a_history_entry()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        var result = await new AppointmentReschedulingService(db, TimeProvider.System)
            .RescheduleAsync(fixture.AppointmentId, new RescheduleAppointmentRequest(fixture.OriginalStylistId, TimeSpan.FromHours(9)), new AppointmentChangeActor("reception-1", "Lễ tân"));

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(0, await db.AppointmentChangeLogs.CountAsync());
    }

    [TestMethod]
    public async Task Reschedule_without_customer_email_does_not_enqueue_notification()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);
        (await db.Customers.SingleAsync()).Email = null;
        await db.SaveChangesAsync();

        var result = await new AppointmentReschedulingService(db, TimeProvider.System)
            .RescheduleAsync(fixture.AppointmentId, new RescheduleAppointmentRequest(fixture.AlternateStylistId, TimeSpan.FromHours(11)));

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(0, await db.AppointmentChangeEmails.CountAsync());
        Assert.AreEqual("NotRequired", (await db.AppointmentChangeLogs.SingleAsync()).EmailStatus);
    }

    [TestMethod]
    public async Task Preview_validates_without_changing_appointment_or_creating_notifications()
    {
        await using var db = CreateDb();
        var fixture = await SeedAsync(db);

        var result = await new AppointmentReschedulingService(db, TimeProvider.System)
            .PreviewAsync(fixture.AppointmentId, new RescheduleAppointmentRequest(fixture.AlternateStylistId, TimeSpan.FromHours(11)));

        Assert.IsTrue(result.Succeeded);
        var appointment = await db.Appointments.SingleAsync();
        Assert.AreEqual(fixture.OriginalStylistId, appointment.StylistId);
        Assert.AreEqual(TimeSpan.FromHours(9), appointment.StartTime);
        Assert.AreEqual(0, await db.AppointmentChangeLogs.CountAsync());
        Assert.AreEqual(0, await db.AppointmentChangeEmails.CountAsync());
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());

    private static async Task<(int AppointmentId, int OriginalStylistId, int AlternateStylistId, int UnqualifiedStylistId, DateTime Date)> SeedAsync(ApplicationDbContext db)
    {
        var date = new DateTime(2031, 2, 3);
        var cut = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 100000 };
        var dye = new Service { ServiceName = "Nhuộm", DurationMinutes = 60, Price = 200000 };
        db.Services.AddRange(cut, dye); await db.SaveChangesAsync();
        var original = new Stylist { FullName = "Thợ cũ", Phone = "0900000001", Services = [new StylistService { ServiceId = cut.ServiceId }] };
        var alternate = new Stylist { FullName = "Thợ mới", Phone = "0900000002", Services = [new StylistService { ServiceId = cut.ServiceId }] };
        var unqualified = new Stylist { FullName = "Thợ thiếu kỹ năng", Phone = "0900000003", Services = [new StylistService { ServiceId = dye.ServiceId }] };
        db.Stylists.AddRange(original, alternate, unqualified); await db.SaveChangesAsync();
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = original.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) },
            new WorkSchedule { StylistId = alternate.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) },
            new WorkSchedule { StylistId = unqualified.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) });
        var appointment = new Appointment { Customer = new Customer { FullName = "Khách sửa lịch", Phone = "0900000004", Email = "demo@example.test" }, StylistId = original.StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "Confirmed", BookingReference = "RESCH001", AppointmentServices = [new AppointmentService { ServiceId = cut.ServiceId, Price = cut.Price, DurationMinutes = cut.DurationMinutes }] };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        return (appointment.AppointmentId, original.StylistId, alternate.StylistId, unqualified.StylistId, date);
    }

    private sealed class FakeEmailService : IEmailService
    {
        public string? Recipient { get; private set; }
        public string? StylistName { get; private set; }
        public Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword) => Task.CompletedTask;
        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink) => Task.CompletedTask;
        public Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode) => Task.CompletedTask;
        public Task SendAppointmentChangeEmailAsync(string toEmail, string stylistName, DateTime appointmentDate, TimeSpan startTime)
        {
            Recipient = toEmail; StylistName = stylistName; return Task.CompletedTask;
        }
    }
}
