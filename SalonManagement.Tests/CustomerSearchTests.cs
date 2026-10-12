using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class CustomerSearchTests
{
    [Fact]
    public async Task SearchAsync_MatchesVietnameseNamesWithoutDiacriticsAndIgnoresCase()
    {
        await using var db = CreateDb();
        db.Customers.AddRange(
            Customer(1, "Nguyễn Thị Ánh", "0901111111"),
            Customer(2, "Trần Anh", "0902222222"));
        await db.SaveChangesAsync();

        var result = await new CustomerProfileManagementService(db).SearchAsync("NGUYEN thi anh");

        Assert.Single(result.Results);
        Assert.Equal(1, result.Results[0].CustomerId);
    }

    [Fact]
    public async Task SearchAsync_MatchesFullAndPartialPhoneIncludingCountryCode()
    {
        await using var db = CreateDb();
        db.Customers.Add(Customer(1, "Khách quen", "0901234567"));
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 1,
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = new DateTime(2026, 10, 12),
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(11, 0, 0),
            Status = "Completed"
        });
        await db.SaveChangesAsync();
        var service = new CustomerProfileManagementService(db);

        var fullPhone = (await service.SearchAsync("0901234567")).Results.Single();
        Assert.Equal(1, fullPhone.CustomerId);
        Assert.Equal(1, fullPhone.VisitCount);
        Assert.Equal(1, (await service.SearchAsync("0123")).Results.Single().CustomerId);
        Assert.Equal(1, (await service.SearchAsync("+84 901-23")).Results.Single().CustomerId);
    }

    [Fact]
    public async Task SearchAsync_RanksExactThenPrefixThenPartialAndCapsResults()
    {
        await using var db = CreateDb();
        db.Customers.AddRange(
            Customer(1, "X An Nguyen", "0900000001"),
            Customer(2, "An Nguyen Anh", "0900000002"),
            Customer(3, "An Nguyen", "0900000003"));
        await db.SaveChangesAsync();

        var result = await new CustomerProfileManagementService(db).SearchAsync("An Nguyen");

        Assert.Equal(new[] { 3, 2, 1 }, result.Results.Select(item => item.CustomerId));
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyForNoMatchAndRejectsShortQuery()
    {
        await using var db = CreateDb();
        var service = new CustomerProfileManagementService(db);

        Assert.Empty((await service.SearchAsync("khong co")).Results);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync("a"));
    }

    [Fact]
    public async Task SearchAsync_ReturnsTotalAndOnlyFirst25Matches()
    {
        await using var db = CreateDb();
        db.Customers.AddRange(Enumerable.Range(1, 40).Select(id => Customer(id, $"Khach tim {id:00}", $"09{id:08}")));
        await db.SaveChangesAsync();

        var result = await new CustomerProfileManagementService(db).SearchAsync("khach tim");

        Assert.Equal(40, result.TotalCount);
        Assert.Equal(25, result.Results.Count);
        Assert.True(result.HasMore);
    }

    [Fact]
    public async Task SearchAsync_FindsAndRanksResultsWithinOneSecondAcrossFiveThousandProfiles()
    {
        await using var db = CreateDb();
        db.Customers.AddRange(Enumerable.Range(1, 5_000).Select(id => Customer(
            id,
            id % 500 == 0 ? $"Nguyễn Minh Ánh {id}" : $"Khách hàng {id}",
            $"09{id:08}")));
        await db.SaveChangesAsync();
        var service = new CustomerProfileManagementService(db);

        var stopwatch = Stopwatch.StartNew();
        var result = await service.SearchAsync("nguyen minh anh");
        stopwatch.Stop();

        Assert.Equal(10, result.TotalCount);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Search took {stopwatch.Elapsed.TotalMilliseconds:N0} ms for 5,000 profiles.");
    }

    private static Customer Customer(int id, string name, string phone) => new()
    {
        CustomerId = id,
        FullName = name,
        Phone = phone,
        NormalizedPhone = phone,
        IsActive = true
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
