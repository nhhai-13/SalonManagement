using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Booking;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class StylistAvailabilityTests
{
    private static readonly DateTime Day = new(2030, 1, 7);
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 1, 7, 1, 0, 0, TimeSpan.Zero); // 08:00 Vietnam
    }
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
    private static StylistAvailabilityService Service(ApplicationDbContext db) => new(db, new Clock());

    private static async Task<(int cut, int wash, int dye, int a, int b, int c)> Seed(ApplicationDbContext db)
    {
        await StylistBookingDemoSeed.SeedAsync(db, Day);
        var services = await db.Services.OrderBy(s => s.ServiceId).ToListAsync();
        var stylists = await db.Stylists.OrderBy(s => s.FullName).ToListAsync();
        return (services.Single(s => s.ServiceName.Contains("Cắt")).ServiceId,
            services.Single(s => s.ServiceName.Contains("Gội")).ServiceId,
            services.Single(s => s.ServiceName.Contains("Nhuộm")).ServiceId,
            stylists[0].StylistId, stylists[1].StylistId, stylists[2].StylistId);
    }

    [Fact]
    public async Task AllSkillsRequired_AdditionalServiceNarrowsList_AndInactiveStylistExcluded()
    {
        await using var db = Db(); var x = await Seed(db); var service = Service(db);
        Assert.Equal(new[] { x.a, x.b, x.c }, (await service.GetStylistsAsync([x.cut])).Select(s => s.Id));
        Assert.Equal(new[] { x.a, x.b }, (await service.GetStylistsAsync([x.cut, x.wash, x.cut])).Select(s => s.Id));
        Assert.Equal(x.b, Assert.Single(await service.GetStylistsAsync([x.cut, x.wash, x.dye])).Id);
        (await db.Stylists.FindAsync(x.b))!.IsActive = false; await db.SaveChangesAsync();
        Assert.Empty(await service.GetStylistsAsync([x.cut, x.dye]));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSlotsAsync([x.cut, x.dye], x.a, DateOnly.FromDateTime(Day)));
    }

    [Fact]
    public async Task SlotsBelongOnlyToSelectedStylist_AndUseTotalDuration()
    {
        await using var db = Db(); var x = await Seed(db); var service = Service(db); var date = DateOnly.FromDateTime(Day);
        var a = await service.GetSlotsAsync([x.cut, x.wash], x.a, date);
        var b = await service.GetSlotsAsync([x.cut, x.wash], x.b, date);
        Assert.DoesNotContain(a, s => s.Start == "09:00");
        Assert.Contains(b, s => s.Start == "09:00" && s.End == "10:00");
        Assert.Contains(a, s => s.Start == "10:00" && s.End == "11:00");
        Assert.DoesNotContain(b, s => s.Start == "10:00");
        Assert.Contains(a, s => s.Start == "16:00" && s.End == "17:00");
        Assert.DoesNotContain(a, s => s.Start == "16:15");
        var busy = await db.Appointments.FirstAsync(p => p.StylistId == x.a);
        busy.StartTime = TimeSpan.FromHours(9); busy.EndTime = TimeSpan.FromHours(17); await db.SaveChangesAsync();
        Assert.Empty(await service.GetSlotsAsync([x.cut], x.a, date));
        Assert.NotEmpty(await service.GetSlotsAsync([x.cut], x.b, date));
    }

    [Fact]
    public async Task CancelledBookingsDoNotBlock_ButBreaksClosedDaysAndPastTimesDo()
    {
        await using var db = Db(); var x = await Seed(db); var service = Service(db); var date = DateOnly.FromDateTime(Day);
        var busy = await db.Appointments.FirstAsync(p => p.StylistId == x.a); busy.Status = "Cancelled";
        var shift = await db.WorkSchedules.FirstAsync(s => s.StylistId == x.a); shift.StartTime = TimeSpan.FromHours(7); shift.EndTime = TimeSpan.FromHours(12);
        var hours = await db.BusinessHours.SingleAsync(h => h.DayOfWeek == Day.DayOfWeek); hours.OpensAt = new(7, 0);
        db.WorkSchedules.Add(new() { StylistId = x.a, WorkDate = Day, StartTime = TimeSpan.FromHours(13), EndTime = TimeSpan.FromHours(17) });
        await db.SaveChangesAsync();
        var slots = await service.GetSlotsAsync([x.cut, x.wash], x.a, date);
        Assert.Contains(slots, s => s.Start == "09:00");
        Assert.DoesNotContain(slots, s => string.CompareOrdinal(s.Start, "08:00") <= 0);
        Assert.DoesNotContain(slots, s => string.CompareOrdinal(s.Start, "11:00") > 0 && string.CompareOrdinal(s.Start, "13:00") < 0);
        Assert.Empty(await service.GetSlotsAsync([x.cut], x.a, date.AddDays(-1)));
        hours.IsClosed = true; await db.SaveChangesAsync();
        Assert.Empty(await service.GetSlotsAsync([x.cut], x.a, date));
    }

    [Fact]
    public async Task InvalidSelectionFailsClosed_AndNoScheduleMeansNoSlots()
    {
        await using var db = Db(); var x = await Seed(db); var service = Service(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetStylistsAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetStylistsAsync([x.cut, 99999]));
        Assert.Empty(await service.GetSlotsAsync([x.cut], x.a, DateOnly.FromDateTime(Day.AddDays(8))));
        (await db.Services.FindAsync(x.cut))!.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSlotsAsync([x.cut], x.a, DateOnly.FromDateTime(Day)));
        var controller = new StylistAvailabilityController(service);
        Assert.IsType<BadRequestObjectResult>(await controller.Stylists([x.cut]));
        Assert.IsType<BadRequestObjectResult>(await controller.Slots([x.wash], 0, default));
    }

    [Fact]
    public async Task OverlappingShiftsDoNotDuplicateSlots_AndNonWorkingShiftsDoNotCount()
    {
        await using var db = Db(); var x = await Seed(db); var service = Service(db); var date = DateOnly.FromDateTime(Day);
        db.WorkSchedules.Add(new() { StylistId = x.c, WorkDate = Day, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(15) });
        await db.SaveChangesAsync();
        var slots = await service.GetSlotsAsync([x.cut], x.c, date);
        Assert.Equal(slots.Count, slots.Select(s => s.Start).Distinct().Count());
        Assert.Equal(31, slots.Count); // 09:00 through 16:30
        foreach (var shift in await db.WorkSchedules.Where(s => s.StylistId == x.c).ToListAsync()) shift.Status = "Off";
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetSlotsAsync([x.cut], x.c, date));
    }

    [Fact]
    public async Task DemoSeedIsIdempotent()
    {
        await using var db = Db(); await Seed(db); await StylistBookingDemoSeed.SeedAsync(db, Day);
        Assert.Equal(3, await db.Stylists.CountAsync()); Assert.Equal(14, await db.Appointments.CountAsync());
    }
}
