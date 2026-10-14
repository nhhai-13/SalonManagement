using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class AppointmentDayTypeComparisonTests
{
    [Fact]
    public async Task OctoberCountsDaysAveragesRatesAndPeakTies()
    {
        await using var db = CreateDb();
        db.Set<ShopHoliday>().AddRange(new ShopHoliday { HolidayDate = new(2026, 10, 2), Reason = "Weekday holiday" },
            new ShopHoliday { HolidayDate = new(2026, 10, 3), Reason = "Weekend holiday" });
        for (var i = 0; i < 44; i++)
            db.Appointments.Add(new Appointment { AppointmentDate = new DateTime(2026, 10, 2),
                StartTime = TimeSpan.FromHours(i % 2 == 0 ? 9 : 10), Status = i < 4 ? "NoShow" : i < 6 ? "Cancelled" : "Confirmed" });
        for (var i = 0; i < 10; i++)
            db.Appointments.Add(new Appointment { AppointmentDate = new DateTime(2026, 10, 3),
                StartTime = TimeSpan.FromHours(i < 8 ? 10 : 12), Status = i < 2 ? "NoShow" : i < 3 ? "Cancelled" : "Pending" });
        db.Appointments.Add(new Appointment { AppointmentDate = new DateTime(2026, 10, 4), StartTime = TimeSpan.FromHours(11), Status = "Confirmed" });
        await db.SaveChangesAsync();
        var result = await Get(db, new(2026, 10, 1), new(2026, 10, 31));
        var weekday = result.DayTypes[0]; var saturday = result.DayTypes[1]; var sunday = result.DayTypes[2];
        Assert.Equal(new[] { 22, 5, 4 }, result.DayTypes.Select(g => g.DayCount));
        Assert.Equal(result.Total, result.DayTypes.Sum(g => g.Total));
        Assert.Equal(2m, weekday.AveragePerDay);
        Assert.Equal(9.1m, weekday.NoShowRate); Assert.Equal(4.5m, weekday.CancellationRate);
        Assert.Equal(20m, saturday.NoShowRate); Assert.Equal(10m, saturday.CancellationRate);
        Assert.Equal(0m, sunday.NoShowRate);
        Assert.Equal(new[] { "09:00", "10:00" }, weekday.HourlyBuckets!.Where(b => b.IsPeak).Select(b => b.Start));
        Assert.Equal("10:00", Assert.Single(saturday.HourlyBuckets!, b => b.IsPeak).Start);
        Assert.Equal(1.6m, saturday.HourlyBuckets![10].AveragePerDay);
        Assert.All(result.DayTypes, group => Assert.Equal(group.Total, group.HourlyBuckets!.Sum(bucket => bucket.Count)));
    }

    [Theory]
    [InlineData(2, "weekday")]
    [InlineData(3, "saturday")]
    [InlineData(4, "sunday")]
    public async Task SingleDayHasExactlyOnePresentGroupAndTwoNullDistributions(int day, string key)
    {
        await using var db = CreateDb();
        var date = new DateOnly(2026, 10, day);
        var result = await Get(db, date, date);
        var present = Assert.Single(result.DayTypes, g => g.DayCount > 0);
        Assert.Equal(key, present.Key); Assert.Equal(1, present.DayCount);
        Assert.Equal(0m, present.AveragePerDay); Assert.Null(present.NoShowRate);
        Assert.All(present.HourlyBuckets!, bucket => Assert.False(bucket.IsPeak));
        Assert.All(result.DayTypes.Where(g => g.DayCount == 0), group => {
            Assert.Null(group.HourlyBuckets); Assert.Null(group.AveragePerDay); Assert.Null(group.NoShowRate);
            Assert.StartsWith("Khoảng ngày chưa có", group.EmptyReason);
        });
    }

    [Fact]
    public async Task FridayLateAndSaturdayEarlyUseAppointmentDayOnSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        var customer = new Customer { FullName = "Demo", Phone = "0900000000" }; var stylist = new Stylist { FullName = "Demo" };
        db.Appointments.AddRange(
            new Appointment { Customer = customer, Stylist = stylist, BookingReference = "FRIDAY", AppointmentDate = new DateTime(2026, 10, 2), StartTime = new TimeSpan(23, 30, 0), Status = "NoShow" },
            new Appointment { Customer = customer, Stylist = stylist, BookingReference = "SATURDAY", AppointmentDate = new DateTime(2026, 10, 3), StartTime = new TimeSpan(0, 30, 0), Status = "Cancelled" });
        await db.SaveChangesAsync();
        var result = await Get(db, new(2026, 10, 2), new(2026, 10, 3));
        Assert.Equal(1, result.DayTypes[0].HourlyBuckets![23].Count);
        Assert.Equal(1, result.DayTypes[1].HourlyBuckets![0].Count);
        Assert.Equal(100m, result.DayTypes[0].NoShowRate); Assert.Equal(100m, result.DayTypes[1].CancellationRate);
        Assert.Null(result.DayTypes[2].HourlyBuckets);
    }

    private static Task<AppointmentAnalyticsResponse> Get(ApplicationDbContext db, DateOnly start, DateOnly end) =>
        new AppointmentAnalyticsService(db).GetAsync(new() { StartDate = start, EndDate = end });
    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
}
