using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class CustomerProfileServiceTests
{
    [Fact]
    public async Task ResolveForBooking_CreatesProfileForNewPhone()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        var service = new CustomerProfileService(db);

        var result = await service.ResolveForBookingAsync(
            new ResolveBookingCustomerRequest("Nguyễn An", "0901 234 567", null));

        Assert.True(result.Created);
        Assert.False(result.IsReturningCustomer);
        var customer = await db.Customers.SingleAsync();
        Assert.Equal("Nguyễn An", customer.FullName);
        Assert.Equal("0901 234 567", customer.Phone);
        Assert.Equal("0901234567", customer.NormalizedPhone);
    }

    [Fact]
    public async Task ResolveForBooking_MatchesExistingPhoneWithoutCreatingOrRenamingProfile()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        db.Customers.Add(new Customer
        {
            FullName = "Khách cũ",
            Phone = "0901234567",
            NormalizedPhone = "0901234567"
        });
        await db.SaveChangesAsync();

        var result = await new CustomerProfileService(db).ResolveForBookingAsync(
            new ResolveBookingCustomerRequest("Tên nhập mới", "0901234567", null));

        Assert.True(result.IsReturningCustomer);
        Assert.False(result.Created);
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal("Khách cũ", (await db.Customers.SingleAsync()).FullName);
    }

    [Theory]
    [InlineData("+84 901-234-567", "0901234567")]
    [InlineData("090-123-4567", "0901234567")]
    [InlineData("(090) 123 4567", "0901234567")]
    public async Task ResolveForBooking_MatchesEquivalentPhoneFormats(string existingPhone, string submittedPhone)
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        db.Customers.Add(new Customer
        {
            FullName = "Khách quen",
            Phone = existingPhone,
            NormalizedPhone = CustomerProfileService.NormalizePhone(existingPhone)
        });
        await db.SaveChangesAsync();

        var result = await new CustomerProfileService(db).ResolveForBookingAsync(
            new ResolveBookingCustomerRequest("Khách quen", submittedPhone, null));

        Assert.True(result.IsReturningCustomer);
        Assert.Equal(1, await db.Customers.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567890")]
    [InlineData("090123456")]
    [InlineData("09012345678")]
    [InlineData("0901234567abc")]
    [InlineData("090+1234567")]
    public async Task ResolveForBooking_RejectsMissingOrInvalidPhoneWithoutCreatingProfile(string phone)
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<ArgumentException>(() => new CustomerProfileService(db)
            .ResolveForBookingAsync(new ResolveBookingCustomerRequest("Khách", phone, null)));

        Assert.Empty(await db.Customers.ToListAsync());
    }

    [Fact]
    public async Task ResolveForBooking_ReturnsConflictWhenSeveralProfilesSharePhone()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        db.Customers.AddRange(
            new Customer { FullName = "Hồ sơ 1", Phone = "0901234567", NormalizedPhone = "0901234567" },
            new Customer { FullName = "Hồ sơ 2", Phone = "+84 901 234 567", NormalizedPhone = "0901234567" });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DuplicateCustomerPhoneException>(() => new CustomerProfileService(db)
            .ResolveForBookingAsync(new ResolveBookingCustomerRequest("Khách", "0901234567", null)));

        Assert.Equal(2, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task ResolveForBooking_ConcurrentRequestsForSamePhoneCreateOnlyOneProfile()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var firstDb = CreateDb(databaseName);
        await using var secondDb = CreateDb(databaseName);

        var results = await Task.WhenAll(
            new CustomerProfileService(firstDb).ResolveForBookingAsync(
                new ResolveBookingCustomerRequest("Khách A", "0901234567", null)),
            new CustomerProfileService(secondDb).ResolveForBookingAsync(
                new ResolveBookingCustomerRequest("Khách B", "+84 901 234 567", null)));

        Assert.Single(results, result => result.Created);
        Assert.Single(results, result => result.IsReturningCustomer);
        await using var verifyDb = CreateDb(databaseName);
        Assert.Equal(1, await verifyDb.Customers.CountAsync());
    }

    private static ApplicationDbContext CreateDb(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new ApplicationDbContext(options);
    }
}
