using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class AppointmentLifecycleTests
{
    [TestMethod]
    public async Task Check_in_within_window_marks_arrival_and_lateness()
    {
        await using var db = CreateDb();
        var appointment = AddAppointment("Confirmed", 9);
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var service = new AppointmentLifecycleService(db, new FixedTimeProvider(Local(2031, 2, 3, 9, 20)));

        var result = await service.CheckInAsync(appointment.AppointmentId);
        var saved = await db.Appointments.SingleAsync();

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("Arrived", saved.Status);
        Assert.IsNotNull(saved.ArrivedAt);
        Assert.IsTrue(saved.IsLate);
        Assert.AreEqual(20, saved.LateMinutes);
        Assert.AreEqual(0, await db.StaffNotifications.CountAsync());
    }

    [TestMethod]
    public async Task Check_in_outside_allowed_window_does_not_change_appointment()
    {
        await using var db = CreateDb();
        var appointment = AddAppointment("Confirmed", 9);
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var service = new AppointmentLifecycleService(db, new FixedTimeProvider(Local(2031, 2, 3, 10, 1)));

        var result = await service.CheckInAsync(appointment.AppointmentId);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("Confirmed", (await db.Appointments.SingleAsync()).Status);
    }

    [TestMethod]
    public async Task Check_in_creates_an_unread_notification_for_the_assigned_stylist()
    {
        await using var db = CreateDb();
        var stylist = new Stylist { FullName = "Thợ A", Phone = "0900000001", Email = "stylist@example.test", IsActive = true };
        db.Stylists.Add(stylist); await db.SaveChangesAsync();
        var appointment = AddAppointment("Confirmed", 9);
        appointment.StylistId = stylist.StylistId;
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var service = new AppointmentLifecycleService(db, new FixedTimeProvider(Local(2031, 2, 3, 9, 0)));

        Assert.IsTrue((await service.CheckInAsync(appointment.AppointmentId)).Succeeded);
        var notification = await db.StaffNotifications.SingleAsync();
        Assert.AreEqual("stylist@example.test", notification.RecipientEmail);
        Assert.AreEqual("CheckIn", notification.Type);
        Assert.IsNull(notification.ReadAt);
    }

    [TestMethod]
    public async Task No_show_after_thirty_minutes_can_be_undone_within_fifteen_minutes()
    {
        await using var db = CreateDb();
        var appointment = AddAppointment("Confirmed", 9);
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var clock = new FixedTimeProvider(Local(2031, 2, 3, 9, 31));
        var service = new AppointmentLifecycleService(db, clock);

        var mark = await service.MarkNoShowAsync(appointment.AppointmentId, "reception-1");
        clock.UtcNow = Local(2031, 2, 3, 9, 40);
        var undo = await service.UndoNoShowAsync(appointment.AppointmentId);
        var saved = await db.Appointments.SingleAsync();

        Assert.IsTrue(mark.Succeeded);
        Assert.IsTrue(undo.Succeeded);
        Assert.AreEqual("Confirmed", saved.Status);
        Assert.IsNull(saved.NoShowAt);
        Assert.IsNull(saved.NoShowByUserId);
    }

    [TestMethod]
    public async Task No_show_rejects_early_attempts_and_late_undo()
    {
        await using var db = CreateDb();
        var appointment = AddAppointment("Confirmed", 9);
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var clock = new FixedTimeProvider(Local(2031, 2, 3, 9, 29));
        var service = new AppointmentLifecycleService(db, clock);

        Assert.IsFalse((await service.MarkNoShowAsync(appointment.AppointmentId, "reception-1")).Succeeded);
        clock.UtcNow = Local(2031, 2, 3, 9, 31);
        Assert.IsTrue((await service.MarkNoShowAsync(appointment.AppointmentId, "reception-1")).Succeeded);
        clock.UtcNow = Local(2031, 2, 3, 9, 47);
        Assert.IsFalse((await service.UndoNoShowAsync(appointment.AppointmentId)).Succeeded);
        Assert.AreEqual("NoShow", (await db.Appointments.SingleAsync()).Status);
    }

    private static Appointment AddAppointment(string status, int hour) => new()
    {
        Customer = new Customer { FullName = "Khách", Phone = "0912345678" },
        StylistId = 1,
        AppointmentDate = new DateTime(2031, 2, 3),
        StartTime = TimeSpan.FromHours(hour),
        EndTime = TimeSpan.FromHours(hour + .5),
        BookingReference = "LIFE0001",
        Status = status
    };

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(7)).ToUniversalTime();

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
