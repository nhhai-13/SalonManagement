using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Booking;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class AnyStylistAvailabilityTests
{
    private static readonly DateOnly Date = new(2030, 1, 7);
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 1, 7, 1, 0, 0, TimeSpan.Zero);
    }
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
    private static async Task<(int[] services, int a, int b)> Seed(ApplicationDbContext db)
    {
        await StylistBookingDemoSeed.SeedAsync(db, Date.ToDateTime(TimeOnly.MinValue));
        return (await db.Services.Where(s => s.ServiceName != "Demo Nhuộm tóc").Select(s => s.ServiceId).ToArrayAsync(),
            (await db.Stylists.SingleAsync(s => s.FullName == "Demo Thợ A")).StylistId,
            (await db.Stylists.SingleAsync(s => s.FullName == "Demo Thợ B")).StylistId);
    }

    [Fact]
    public async Task AnyIsSortedDistinctUnion_IncludesSlotsOnlyOneStylistCanDo_WithoutAssigningAnyone()
    {
        await using var db = Db(); var x = await Seed(db); var service = new StylistAvailabilityService(db, new Clock());
        var before = await db.Appointments.CountAsync();
        var a = await service.GetSlotsAsync(x.services, x.a, Date);
        var b = await service.GetSlotsAsync(x.services, x.b, Date);
        var any = await service.GetSlotsAsync(x.services, 0, Date);
        Assert.Equal(a.Concat(b).Distinct().OrderBy(s => s.Start, StringComparer.Ordinal), any);
        Assert.Equal(any.Count, any.Distinct().Count());
        Assert.Contains(new StylistSlot("09:00", "10:00"), any);
        Assert.Contains(new StylistSlot("10:00", "11:00"), any);
        Assert.DoesNotContain(new StylistSlot("09:15", "10:15"), any); // Neither can cover the entire hour.
        Assert.Equal(before, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task FamiliarStylistFullyBooked_AnyStillShowsOtherStylist_AndSwitchingBackStaysEmpty()
    {
        await using var db = Db(); var x = await Seed(db); var service = new StylistAvailabilityService(db, new Clock());
        var day = Date.AddDays(1);
        Assert.Empty(await service.GetSlotsAsync(x.services, x.a, day));
        Assert.Equal(await service.GetSlotsAsync(x.services, x.b, day), await service.GetSlotsAsync(x.services, 0, day));
        Assert.NotEmpty(await service.GetSlotsAsync(x.services, 0, day));
        Assert.Empty(await service.GetSlotsAsync(x.services, x.a, day));
    }

    [Fact]
    public async Task UnqualifiedOrInactiveFreeStylistsCannotSupplySlots_AndNoQualifiedStylistMeansEmpty()
    {
        await using var db = Db(); var x = await Seed(db); var service = new StylistAvailabilityService(db, new Clock());
        (await db.Stylists.FindAsync(x.b))!.IsActive = false;
        var appointment = await db.Appointments.FirstAsync(a => a.StylistId == x.a);
        appointment.StartTime = TimeSpan.FromHours(9); appointment.EndTime = TimeSpan.FromHours(17);
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetSlotsAsync(x.services, 0, Date)); // C is free but lacks wash.
        (await db.Stylists.FindAsync(x.a))!.IsActive = false; await db.SaveChangesAsync();
        Assert.Empty(await service.GetSlotsAsync(x.services, 0, Date));
    }

    [Fact]
    public async Task CannotJoinDifferentStylistsShiftsToCoverOneService()
    {
        await using var db = Db(); var x = await Seed(db); var service = new StylistAvailabilityService(db, new Clock());
        db.Appointments.RemoveRange(db.Appointments);
        db.WorkSchedules.RemoveRange(db.WorkSchedules);
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = x.a, WorkDate = Date.ToDateTime(TimeOnly.MinValue), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5) },
            new WorkSchedule { StylistId = x.b, WorkDate = Date.ToDateTime(TimeOnly.MinValue), StartTime = TimeSpan.FromHours(9.5), EndTime = TimeSpan.FromHours(10) });
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetSlotsAsync(x.services, 0, Date));
    }

    [Fact]
    public async Task ExistingDemoGetsFullDayScenarioOnceWithoutRewritingOriginalAppointment()
    {
        await using var db = Db(); var x = await Seed(db);
        var fullDay = Date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var original = await db.Appointments.SingleAsync(a => a.StylistId == x.a && a.AppointmentDate == fullDay);
        original.EndTime = TimeSpan.FromHours(10); await db.SaveChangesAsync();
        await StylistBookingDemoSeed.SeedAsync(db, Date.ToDateTime(TimeOnly.MinValue));
        await StylistBookingDemoSeed.SeedAsync(db, Date.ToDateTime(TimeOnly.MinValue));
        Assert.Equal(TimeSpan.FromHours(10), original.EndTime);
        Assert.Equal(15, await db.Appointments.CountAsync());
        Assert.Empty(await new StylistAvailabilityService(db, new Clock()).GetSlotsAsync(x.services, x.a, Date.AddDays(1)));
    }

    [Fact]
    public async Task AnyStillHonorsClosedDayAndValidatesServices_ExplicitSelectionRequired()
    {
        await using var db = Db(); var x = await Seed(db); var service = new StylistAvailabilityService(db, new Clock());
        var controller = new StylistAvailabilityController(service);
        Assert.IsType<OkObjectResult>(await controller.Slots(x.services, 0, Date));
        Assert.IsType<BadRequestObjectResult>(await controller.Slots(x.services, null, Date));
        Assert.IsType<BadRequestObjectResult>(await controller.Slots(x.services, -1, Date));
        Assert.IsType<BadRequestObjectResult>(await controller.Slots([999999], 0, Date));
        (await db.BusinessHours.SingleAsync(h => h.DayOfWeek == Date.DayOfWeek)).IsClosed = true;
        await db.SaveChangesAsync();
        Assert.Empty(await service.GetSlotsAsync(x.services, 0, Date));
        Assert.Empty(await service.GetSlotsAsync(x.services, 0, Date.AddDays(-1)));
        (await db.Services.FindAsync(x.services[0]))!.IsActive = false; await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.Slots(x.services, 0, Date));
    }
}
