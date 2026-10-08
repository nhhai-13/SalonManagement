using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;
public class CheckInTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 10, 0, 0, TimeSpan.FromHours(7));
    private class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    }
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Microsoft.AspNetCore.Http.HttpContextAccessor());
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
        Assert.Equal(allowed, CheckInService.CanCheckIn(a, Start.AddMinutes(minutes)));
        if (!allowed) await Assert.ThrowsAsync<InvalidOperationException>(() => new CheckInService(db, new Clock { Now = Start.AddMinutes(minutes) }).Apply(a.AppointmentId, "check-in", "desk"));
    }
    [Theory] [InlineData(15, 0)] [InlineData(16, 16)]
    public async Task CheckInPersistsArrivalNotificationAndAuditOnce(int minutes, int late)
    {
        await using var db = Db(); var a = await Seed(db); var clock = new Clock { Now = Start.AddMinutes(minutes) };
        var svc = new CheckInService(db, clock);
        await svc.Apply(a.AppointmentId, "check-in", "desk");
        Assert.Equal(AppointmentStatuses.Arrived, a.Status); Assert.Equal(clock.Now, a.CheckedInAt);
        Assert.Equal(late, a.LateMinutes); Assert.Single(db.StylistNotifications);
        Assert.Equal(a.StylistId, db.StylistNotifications.Single().StylistId);
        Assert.Equal("desk", db.AppointmentAudits.Single().ActorId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.Apply(a.AppointmentId, "check-in", "desk"));
        Assert.Single(db.StylistNotifications); Assert.Single(db.AppointmentAudits);
    }
    [Theory] [InlineData("Pending")] [InlineData("Cancelled")] [InlineData("Completed")] [InlineData("NoShow")]
    public async Task CheckInRejectsOtherStatuses(string status)
    {
        await using var db = Db(); var a = await Seed(db); a.Status = status; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CheckInService(db, new Clock()).Apply(a.AppointmentId, "check-in", "desk"));
        Assert.Empty(db.AppointmentAudits); Assert.Empty(db.StylistNotifications);
    }
    [Fact]
    public async Task RelationalWriteIsAtomicAndStaleVersionIsRejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor()); await db.Database.EnsureCreatedAsync(); var a = await Seed(db);
        await using var stale = new ApplicationDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor()); var old = await stale.Appointments.SingleAsync();
        await new CheckInService(db, new Clock()).Apply(a.AppointmentId, "check-in", "desk");
        old.Status = "NoShow"; old.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        Assert.Single(db.AppointmentAudits); Assert.Single(db.StylistNotifications);
    }
}
