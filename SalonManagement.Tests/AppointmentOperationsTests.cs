using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;
public class AppointmentOperationsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 10, 0, 0, TimeSpan.FromHours(7));
    private class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<Appointment> Seed(ApplicationDbContext db)
    {
        var a = new Appointment { AppointmentDate = Start.Date, StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11), Status = AppointmentStatuses.Confirmed,
            Customer = new Customer { FullName = "Khách", Phone = "0901234567" }, Stylist = new Stylist { FullName = "Thợ" } };
        db.Add(a); await db.SaveChangesAsync(); return a;
    }
    [Theory]
    [InlineData(-61, false)] [InlineData(-60, true)] [InlineData(60, true)] [InlineData(61, false)]
    public async Task CheckInWindow(int minutes, bool allowed)
    {
        await using var db = Db(); var a = await Seed(db);
        Assert.Equal(allowed, AppointmentOperationsService.CanCheckIn(a, Start.AddMinutes(minutes)));
        if (!allowed) await Assert.ThrowsAsync<InvalidOperationException>(() => new AppointmentOperationsService(db, new Clock { Now = Start.AddMinutes(minutes) }).Apply(a.AppointmentId, "check-in", "desk"));
    }
    [Theory] [InlineData(15, 0)] [InlineData(16, 16)]
    public async Task CheckInPersistsArrivalNotificationAndAuditOnce(int minutes, int late)
    {
        await using var db = Db(); var a = await Seed(db); var clock = new Clock { Now = Start.AddMinutes(minutes) };
        var svc = new AppointmentOperationsService(db, clock);
        await svc.Apply(a.AppointmentId, "check-in", "desk");
        Assert.Equal(AppointmentStatuses.Arrived, a.Status); Assert.Equal(clock.Now, a.CheckedInAt);
        Assert.Equal(late, a.LateMinutes); Assert.Single(db.StylistNotifications);
        Assert.Equal(a.StylistId, db.StylistNotifications.Single().StylistId);
        Assert.Equal("desk", db.AppointmentAudits.Single().ActorId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.Apply(a.AppointmentId, "check-in", "desk"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.Apply(a.AppointmentId, "no-show", "desk"));
        Assert.Single(db.StylistNotifications); Assert.Single(db.AppointmentAudits);
    }
    [Theory] [InlineData(30, false)] [InlineData(31, true)]
    public async Task NoShowWindow(int minutes, bool allowed)
    {
        await using var db = Db(); var a = await Seed(db);
        Assert.Equal(allowed, AppointmentOperationsService.CanMarkNoShow(a, Start.AddMinutes(minutes)));
    }
    [Theory] [InlineData(15, true)] [InlineData(16, false)]
    public async Task UndoWindowAndPhoneHistory(int minutes, bool allowed)
    {
        await using var db = Db(); var a = await Seed(db); var clock = new Clock { Now = Start.AddMinutes(31) };
        var svc = new AppointmentOperationsService(db, clock);
        await svc.Apply(a.AppointmentId, "no-show", "desk");
        Assert.Empty(await svc.OccupiedSlots(a.StylistId, a.AppointmentDate)); Assert.False(AppointmentStatuses.BlocksTime(a.Status)); Assert.Equal(1, await svc.NoShowCount(a.Customer.Phone));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.Apply(a.AppointmentId, "no-show", "desk"));
        clock.Now = clock.Now.AddMinutes(minutes);
        if (allowed) {
            await svc.Apply(a.AppointmentId, "undo-no-show", "desk2");
            Assert.Equal(AppointmentStatuses.Confirmed, a.Status); Assert.Equal(0, await svc.NoShowCount(a.Customer.Phone));
            Assert.Equal(2, db.AppointmentAudits.Count()); Assert.Equal("desk2", db.AppointmentAudits.OrderBy(x => x.Id).Last().ActorId);
        } else {
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.Apply(a.AppointmentId, "undo-no-show", "desk"));
            Assert.Equal(1, await svc.NoShowCount(a.Customer.Phone)); Assert.Single(db.AppointmentAudits);
        }
    }
    [Fact]
    public async Task UndoRejectsRebookedSlot()
    {
        await using var db = Db(); var a = await Seed(db); var clock = new Clock { Now = Start.AddMinutes(31) };
        var svc = new AppointmentOperationsService(db, clock); await svc.Apply(a.AppointmentId, "no-show", "desk");
        db.Add(new Appointment { CustomerId = a.CustomerId, StylistId = a.StylistId, AppointmentDate = a.AppointmentDate,
            StartTime = TimeSpan.FromHours(10.5), EndTime = TimeSpan.FromHours(11.5), Status = "Confirmed" }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.Apply(a.AppointmentId, "undo-no-show", "desk"));
        Assert.Equal("NoShow", a.Status); Assert.Single(db.AppointmentAudits);
    }
    [Theory] [InlineData("Pending")] [InlineData("Cancelled")] [InlineData("Completed")] [InlineData("NoShow")]
    public async Task CheckInRejectsOtherStatuses(string status)
    {
        await using var db = Db(); var a = await Seed(db); a.Status = status; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AppointmentOperationsService(db, new Clock()).Apply(a.AppointmentId, "check-in", "desk"));
        Assert.Empty(db.AppointmentAudits); Assert.Empty(db.StylistNotifications);
    }
    [Fact]
    public async Task HistoryAggregatesCustomersWithSamePhone()
    {
        await using var db = Db(); var a = await Seed(db); var b = await Seed(db);
        var svc = new AppointmentOperationsService(db, new Clock { Now = Start.AddMinutes(31) });
        await svc.Apply(a.AppointmentId, "no-show", "desk"); await svc.Apply(b.AppointmentId, "no-show", "desk");
        Assert.Equal(2, await svc.NoShowCount(a.Customer.Phone));
    }
    [Fact]
    public async Task RelationalWriteIsAtomicAndStaleVersionIsRejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options); await db.Database.EnsureCreatedAsync(); var a = await Seed(db);
        await using var stale = new ApplicationDbContext(options); var old = await stale.Appointments.SingleAsync();
        await new AppointmentOperationsService(db, new Clock()).Apply(a.AppointmentId, "check-in", "desk");
        old.Status = "NoShow"; old.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        Assert.Single(db.AppointmentAudits); Assert.Single(db.StylistNotifications);
    }
    [Fact]
    public void OperationsRequireFrontDeskAndStylistInboxIsRestricted()
    {
        foreach (var type in new[] { typeof(AppointmentsController), typeof(AppointmentOperationsController) })
            Assert.Equal(RoleGroups.FrontDesk, type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal(UserRoles.Stylist, typeof(StylistNotificationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
    }
}
