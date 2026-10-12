using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public sealed class DevelopmentBookingScheduleSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesUpcomingShiftsAndIsIdempotent()
    {
        await using var db = CreateDb();
        db.Stylists.AddRange(
            new Stylist { StylistId = 1, FullName = "Đang làm", Phone = "0900000001", IsActive = true },
            new Stylist { StylistId = 2, FullName = "Đã nghỉ", Phone = "0900000002", IsActive = false });
        await db.SaveChangesAsync();

        await DevelopmentBookingScheduleSeeder.SeedAsync(db, new DateOnly(2026, 10, 12));
        await DevelopmentBookingScheduleSeeder.SeedAsync(db, new DateOnly(2026, 10, 12));

        var schedules = await db.WorkSchedules.ToListAsync();
        Assert.Equal(14, schedules.Count);
        Assert.All(schedules, schedule =>
        {
            Assert.Equal(1, schedule.StylistId);
            Assert.Equal("Working", schedule.Status);
            Assert.Equal(new TimeSpan(8, 0, 0), schedule.StartTime);
            Assert.Equal(new TimeSpan(17, 0, 0), schedule.EndTime);
        });
    }

    [Fact]
    public async Task SeedAsync_PreservesExistingShiftAndDayOff()
    {
        await using var db = CreateDb();
        db.Stylists.Add(new Stylist { StylistId = 1, FullName = "Thợ A", Phone = "0900000001", IsActive = true });
        db.WorkSchedules.Add(new WorkSchedule
        {
            StylistId = 1,
            WorkDate = new DateOnly(2026, 10, 13).ToDateTime(TimeOnly.MinValue),
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Off",
            Notes = "Ngày nghỉ"
        });
        await db.SaveChangesAsync();

        await DevelopmentBookingScheduleSeeder.SeedAsync(db, new DateOnly(2026, 10, 12));

        var schedules = await db.WorkSchedules.OrderBy(item => item.WorkDate).ToListAsync();
        Assert.Equal(14, schedules.Count);
        Assert.Equal("Off", schedules[0].Status);
        Assert.Equal("Ngày nghỉ", schedules[0].Notes);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
