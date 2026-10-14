using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class AppointmentAnalyticsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public async Task HourlyBucketsCountStartTimeOnceWithAllStatusesAndEmptyHours(int days)
    {
        await using var db = CreateDb();
        var day = new DateTime(2026, 1, 1);
        foreach (var item in new[] {
            (TimeSpan.Zero, "Pending"), (new TimeSpan(9, 59, 0), "Cancelled"),
            (TimeSpan.FromHours(10), "NoShow"), (TimeSpan.FromHours(10), "Confirmed"),
            (new TimeSpan(23, 59, 0), "Completed") })
            db.Appointments.Add(new Appointment { AppointmentDate = day, StartTime = item.Item1,
                EndTime = item.Item1.Add(TimeSpan.FromHours(3)), Status = item.Item2 });
        db.Appointments.Add(new Appointment { AppointmentDate = day.AddDays(days), StartTime = TimeSpan.FromHours(10) });
        await db.SaveChangesAsync();
        var report = await new AppointmentAnalyticsService(db).GetAsync(new() {
            StartDate = DateOnly.FromDateTime(day), EndDate = DateOnly.FromDateTime(day.AddDays(days - 1)) });
        Assert.Equal(24, report.HourlyBuckets.Count);
        Assert.Equal(5, report.Total);
        Assert.Equal(report.Total, report.HourlyBuckets.Sum(bucket => bucket.Count));
        Assert.Equal(1, report.HourlyBuckets[0].Count);
        Assert.Equal(1, report.HourlyBuckets[9].Count);
        Assert.Equal(2, report.HourlyBuckets[10].Count);
        Assert.Equal(0, report.HourlyBuckets[11].Count);
        Assert.Equal(1, report.HourlyBuckets[23].Count);
        Assert.Equal("24:00", report.HourlyBuckets[23].End);
        Assert.Equal(20m, report.NoShowRate);
        Assert.Equal(20m, report.CancellationRate);
    }

    [Fact]
    public async Task InvalidStoredTimesAreReportedWithoutSilentlyDroppingAppointments()
    {
        await using var db = CreateDb();
        db.Appointments.AddRange(new Appointment { AppointmentDate = new DateTime(2026, 9, 1), StartTime = TimeSpan.FromDays(1) },
            new Appointment { AppointmentDate = new DateTime(2026, 9, 1), StartTime = TimeSpan.FromMinutes(-1) });
        await db.SaveChangesAsync();
        var report = await new AppointmentAnalyticsService(db).GetAsync(Range());
        Assert.Equal(2, report.UnassignedTimeCount);
        Assert.Equal(report.Total, report.HourlyBuckets.Sum(bucket => bucket.Count) + report.UnassignedTimeCount);
    }
    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Admin", false)]
    [InlineData("Receptionist", false)]
    [InlineData("Stylist", false)]
    [InlineData("Customer", false)]
    [InlineData(null, false)]
    public async Task AuthorizationPolicyRejectsEveryOtherRole(string? role, bool expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        using var provider = services.BuildServiceProvider();
        var attribute = Assert.Single(typeof(AppointmentAnalyticsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), [attribute]);
        var identity = role is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test");
        var result = await provider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(new ClaimsPrincipal(identity), null, policy!);
        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task RelationalQueryIncludesLastDayAndExcludesFollowingMidnight()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection).Options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        var customer = new Customer { FullName = "Test customer", Phone = "0900000000" };
        var stylist = new Stylist { FullName = "Test stylist" };
        db.Appointments.AddRange(
            new Appointment { Customer = customer, Stylist = stylist, BookingReference = "TEST-1",
                AppointmentDate = new DateTime(2026, 9, 1), Status = "NoShow" },
            new Appointment { Customer = customer, Stylist = stylist, BookingReference = "TEST-2",
                AppointmentDate = new DateTime(2026, 9, 30), StartTime = new TimeSpan(23, 59, 0), Status = "Cancelled" },
            new Appointment { Customer = customer, Stylist = stylist, BookingReference = "TEST-3",
                AppointmentDate = new DateTime(2026, 10, 1), Status = "Pending" });
        await db.SaveChangesAsync();
        var report = await new AppointmentAnalyticsService(db).GetAsync(Range());
        Assert.Equal(2, report.Total);
        Assert.Equal(50m, report.NoShowRate);
        Assert.Equal(50m, report.CancellationRate);
        Assert.Equal(1, report.HourlyBuckets[0].Count);
        Assert.Equal(1, report.HourlyBuckets[23].Count);
    }

    [Theory]
    [InlineData(80, 6, 4, 7.5, 5.0)]
    [InlineData(3, 1, 2, 33.3, 66.7)]
    [InlineData(2, 1, 0, 50.0, 0.0)]
    [InlineData(8, 1, 0, 12.5, 0.0)]
    [InlineData(16, 1, 0, 6.3, 0.0)]
    [InlineData(20, 0, 0, 0.0, 0.0)]
    public async Task CountsAndRoundsAllStatuses(int total, int noShow, int cancelled, double expectedNoShow, double expectedCancelled)
    {
        await using var db = CreateDb();
        for (var i = 0; i < total; i++)
            db.Appointments.Add(new Appointment
            {
                AppointmentDate = new DateTime(2026, 9, 10),
                Status = i < noShow ? "NoShow" : i < noShow + cancelled ? "Cancelled" : "Pending"
            });
        await db.SaveChangesAsync();
        var result = await new AppointmentAnalyticsService(db).GetAsync(Range());
        Assert.Equal(total, result.Total);
        Assert.Equal(noShow, result.NoShowCount);
        Assert.Equal(cancelled, result.CancelledCount);
        Assert.Equal((decimal)expectedNoShow, result.NoShowRate);
        Assert.Equal((decimal)expectedCancelled, result.CancellationRate);
        Assert.Null(result.EmptyReason);
        Assert.Equal(total < 20, result.IsSmallSample);
    }

    [Fact]
    public async Task EmptyRangeReturnsNullRatesWithReason()
    {
        await using var db = CreateDb();
        var result = await new AppointmentAnalyticsService(db).GetAsync(Range());
        Assert.Null(result.NoShowRate);
        Assert.Null(result.CancellationRate);
        Assert.Equal("Không có lịch hẹn trong khoảng ngày này", result.EmptyReason);
        Assert.False(result.IsSmallSample);
        Assert.Equal(24, result.HourlyBuckets.Count);
        Assert.All(result.HourlyBuckets, bucket => Assert.Equal(0, bucket.Count));
    }

    [Fact]
    public async Task IncludesBothBoundaryDaysButNotNextMidnightAndCountsServicesOnce()
    {
        await using var db = CreateDb();
        db.Appointments.AddRange(
            new Appointment { AppointmentDate = new DateTime(2026, 9, 1), StartTime = TimeSpan.Zero,
                CreatedAt = new DateTime(2026, 8, 1), Status = "NoShow",
                AppointmentServices = [new() { ServiceId = 1 }, new() { ServiceId = 2 }] },
            new Appointment { AppointmentDate = new DateTime(2026, 9, 30), StartTime = new TimeSpan(23, 59, 0), Status = "Canceled" },
            new Appointment { AppointmentDate = new DateTime(2026, 10, 1), StartTime = TimeSpan.Zero, Status = "NoShow" },
            new Appointment { AppointmentDate = new DateTime(2026, 8, 31), Status = "NoShow" });
        await db.SaveChangesAsync();
        var result = await new AppointmentAnalyticsService(db).GetAsync(Range());
        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.NoShowCount);
        Assert.Equal(1, result.CancelledCount);
    }

    [Theory]
    [InlineData(null, "2026-09-30", "StartDate")]
    [InlineData("2026-09-01", null, "EndDate")]
    [InlineData("2026-09-30", "2026-09-01", "EndDate")]
    [InlineData("2026-01-01", "2027-01-02", "EndDate")]
    public async Task InvalidRangesAreRejected(string? start, string? end, string field)
    {
        await using var db = CreateDb();
        var request = new AppointmentAnalyticsRequest
        {
            StartDate = start is null ? null : DateOnly.Parse(start),
            EndDate = end is null ? null : DateOnly.Parse(end)
        };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, e => e.MemberNames.Contains(field));
        await Assert.ThrowsAsync<ValidationException>(() => new AppointmentAnalyticsService(db).GetAsync(request));
    }

    [Fact]
    public async Task MaximumInclusiveRangeIsAllowed()
    {
        await using var db = CreateDb();
        var start = new DateOnly(2026, 1, 1);
        await new AppointmentAnalyticsService(db).GetAsync(new() { StartDate = start, EndDate = start.AddDays(365) });
    }

    [Fact]
    public async Task ApiReturnsValidationProblemForInvalidModelState()
    {
        await using var db = CreateDb();
        var controller = new AppointmentAnalyticsController(new(db), TimeProvider.System);
        controller.ModelState.AddModelError("EndDate", "Invalid range");
        var result = Assert.IsType<BadRequestObjectResult>(await controller.Get(Range(), default));
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void PageAndApiRequireOwnerJwtAuthentication()
    {
        var authorization = Assert.Single(typeof(AppointmentAnalyticsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(UserRoles.Owner, authorization.Roles);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, authorization.AuthenticationSchemes);
        Assert.All(typeof(AppointmentAnalyticsController).GetMethods()
            .Where(m => m.Name is "Index" or "Get"), m => Assert.Empty(m.GetCustomAttributes(typeof(AllowAnonymousAttribute), true)));
    }

    private static AppointmentAnalyticsRequest Range() => new()
    {
        StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 30)
    };

    private static ApplicationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new HttpContextAccessor());
}
