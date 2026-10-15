using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Booking;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class StylistAssignmentTests
{
    private static readonly DateOnly Date = new(2030, 1, 7);
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 1, 7, 1, 0, 0, TimeSpan.Zero);
    }
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
    private static StylistAvailabilityService Service(ApplicationDbContext db) => new(db, new Clock());
    private static async Task Seed(ApplicationDbContext db)
    {
        var cut = new Service { ServiceId = 1, ServiceName = "Cắt", DurationMinutes = 30, Price = 100000 };
        var wash = new Service { ServiceId = 2, ServiceName = "Gội", DurationMinutes = 30, Price = 50000 };
        db.AddRange(cut, wash);
        for (var id = 1; id <= 3; id++)
            db.Stylists.Add(new() { StylistId = id, FullName = $"Thợ {id}", Services = [new() { Service = cut }, new() { Service = wash }],
                WorkSchedules = [new() { WorkDate = Date.ToDateTime(TimeOnly.MinValue), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17) }] });
        db.BusinessHours.Add(new() { DayOfWeek = Date.DayOfWeek, OpensAt = new(9, 0), ClosesAt = new(17, 0) });
        await db.SaveChangesAsync();
    }
    private static void Book(ApplicationDbContext db, int stylist, int hour, string status = "Confirmed", int dayOffset = 0, int minutes = 30)
        => db.Appointments.Add(new() { StylistId = stylist, Customer = new() { FullName = "Test" },
            AppointmentDate = Date.AddDays(dayOffset).ToDateTime(TimeOnly.MinValue),
            StartTime = TimeSpan.FromHours(hour), EndTime = TimeSpan.FromHours(hour) + TimeSpan.FromMinutes(minutes), Status = status });

    [Fact]
    public async Task ThreeFreeStylists_ChoosesFewestAppointments_AndRecomputesAfterChanges()
    {
        await using var db = Db(); await Seed(db);
        Book(db, 1, 9); Book(db, 1, 10); Book(db, 2, 9, "Pending"); await db.SaveChangesAsync();
        var service = Service(db);
        Assert.Equal(3, (await service.AssignAsync([1, 2], 0, Date, new(14, 0)))!.StylistId);
        Book(db, 3, 9); Book(db, 3, 10); await db.SaveChangesAsync();
        Assert.Equal(2, (await service.AssignAsync([1, 2], 0, Date, new(14, 0)))!.StylistId);
    }

    [Fact]
    public async Task BusyLeastLoadedStylistIsExcluded_OnlyFreeStylistIsAssigned_ThenNoCandidate()
    {
        await using var db = Db(); await Seed(db);
        Book(db, 1, 14); Book(db, 2, 9); Book(db, 2, 10); Book(db, 3, 9); Book(db, 3, 10); Book(db, 3, 11);
        await db.SaveChangesAsync();
        Assert.Equal(2, (await Service(db).AssignAsync([1, 2], 0, Date, new(14, 0)))!.StylistId);
        Book(db, 2, 14); await db.SaveChangesAsync();
        Assert.Equal(3, (await Service(db).AssignAsync([1, 2], 0, Date, new(14, 0)))!.StylistId);
        Book(db, 3, 14); await db.SaveChangesAsync();
        Assert.Null(await Service(db).AssignAsync([1, 2], 0, Date, new(14, 0)));
    }

    [Fact]
    public async Task EqualCountsUseLowestId_CancelledCompletedAndOtherDaysDoNotCount()
    {
        await using var db = Db(); await Seed(db);
        Book(db, 1, 14, "Cancelled"); Book(db, 1, 9, "Completed");
        Book(db, 1, 10, dayOffset: -1); Book(db, 1, 11, dayOffset: 1); await db.SaveChangesAsync();
        var assigned = await Service(db).AssignAsync([1, 2], 0, Date, new(14, 0));
        Assert.Equal(1, assigned!.StylistId); Assert.Equal("15:00", assigned.End);
        Book(db, 1, 10, "Pending"); await db.SaveChangesAsync();
        Assert.Equal(2, (await Service(db).AssignAsync([1, 2], 0, Date, new(14, 0)))!.StylistId);
    }

    [Fact]
    public async Task RankingUsesCountInsteadOfDuration_AndSpecificChoiceIsPreserved()
    {
        await using var db = Db(); await Seed(db);
        Book(db, 1, 9, minutes: 180); Book(db, 2, 9, minutes: 15); Book(db, 2, 10, minutes: 15);
        (await db.Stylists.FindAsync(3))!.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(1, (await Service(db).AssignAsync([1, 2], 0, Date, new(14, 0)))!.StylistId);
        Assert.Equal(2, (await Service(db).AssignAsync([1, 2], 2, Date, new(14, 0)))!.StylistId);
    }

    [Fact]
    public async Task MissingSkillInactiveAndInvalidSlotNeverAssigned()
    {
        await using var db = Db(); await Seed(db);
        db.StylistServices.Remove(await db.StylistServices.SingleAsync(s => s.StylistId == 1 && s.ServiceId == 2));
        (await db.Stylists.FindAsync(2))!.IsActive = false; Book(db, 3, 9); await db.SaveChangesAsync();
        Assert.Equal(3, (await Service(db).AssignAsync([1, 2], 0, Date, new(14, 0)))!.StylistId);
        Assert.Null(await Service(db).AssignAsync([1, 2], 0, Date, new(16, 30)));
        Assert.Null(await Service(db).AssignAsync([1, 2], 0, Date, new(14, 1)));
        Assert.Null(await Service(db).AssignAsync([1, 2], 0, Date, new(14, 0, 1)));
        Assert.Null(await Service(db).AssignAsync([1, 2], 0, Date.AddDays(-1), new(14, 0)));
    }

    [Fact]
    public async Task EndpointRevalidatesSelection_ReportsConflict_AndDoesNotPretendToReserve()
    {
        await using var db = Db(); await Seed(db); var controller = new StylistAvailabilityController(Service(db));
        Assert.IsType<BadRequestObjectResult>(await controller.Assignment([1], null, Date, new(14, 0)));
        Assert.IsType<BadRequestObjectResult>(await controller.Assignment([1], 0, Date, null));
        Assert.IsType<OkObjectResult>(await controller.Assignment([1], 0, Date, new(14, 0)));
        Assert.Empty(db.Appointments);
        for (var id = 1; id <= 3; id++) Book(db, id, 14);
        await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await controller.Assignment([1], 0, Date, new(14, 0)));
        (await db.Services.FindAsync(1))!.IsActive = false; await db.SaveChangesAsync();
        Assert.IsType<BadRequestObjectResult>(await controller.Assignment([1], 0, Date, new(14, 0)));
    }
}
