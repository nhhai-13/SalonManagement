using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;
public class NoShowTests
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
    [Theory] [InlineData(30, false)] [InlineData(31, true)]
    public async Task NoShowWindow(int minutes, bool allowed)
    {
        await using var db = Db(); var a = await Seed(db);
        Assert.Equal(allowed, NoShowService.CanMarkNoShow(a, Start.AddMinutes(minutes)));
    }
    [Theory] [InlineData(15, true)] [InlineData(16, false)]
    public async Task UndoWindowAndPhoneHistory(int minutes, bool allowed)
    {
        await using var db = Db(); var a = await Seed(db); var clock = new Clock { Now = Start.AddMinutes(31) };
        var svc = new NoShowService(db, clock);
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
        var svc = new NoShowService(db, clock); await svc.Apply(a.AppointmentId, "no-show", "desk");
        db.Add(new Appointment { CustomerId = a.CustomerId, StylistId = a.StylistId, AppointmentDate = a.AppointmentDate,
            StartTime = TimeSpan.FromHours(10.5), EndTime = TimeSpan.FromHours(11.5), Status = "Confirmed" }); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.Apply(a.AppointmentId, "undo-no-show", "desk"));
        Assert.Equal("NoShow", a.Status); Assert.Single(db.AppointmentAudits);
    }
    [Fact]
    public async Task HistoryAggregatesCustomersWithSamePhone()
    {
        await using var db = Db(); var a = await Seed(db); var b = await Seed(db);
        var svc = new NoShowService(db, new Clock { Now = Start.AddMinutes(31) });
        await svc.Apply(a.AppointmentId, "no-show", "desk"); await svc.Apply(b.AppointmentId, "no-show", "desk");
        Assert.Equal(2, await svc.NoShowCount(a.Customer.Phone));
    }
}
