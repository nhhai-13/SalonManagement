using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class CustomerProfileSearchTests
{
    [TestMethod]
    public async Task Search_finds_exact_phone_and_equivalent_plus_84_format()
    {
        await using var db = CreateDb();
        var customer = AddCustomer("Nguyễn Văn Minh", "0912345678");
        db.Customers.Add(customer); await db.SaveChangesAsync();
        var search = new CustomerProfileSearchService(db);

        var byStoredPhone = await search.SearchAsync("0912345678");
        var byInternationalPhone = await search.SearchAsync("+84 912 345 678");

        Assert.AreEqual(customer.CustomerId, byStoredPhone.Results.Single().CustomerId);
        Assert.AreEqual(customer.CustomerId, byInternationalPhone.Results.Single().CustomerId);
    }

    [TestMethod]
    public async Task Search_matches_full_partial_unaccented_and_case_insensitive_names_and_phone_fragments()
    {
        await using var db = CreateDb();
        var customer = AddCustomer("Nguyễn Văn Minh", "0912345678");
        db.Customers.Add(customer); await db.SaveChangesAsync();
        var search = new CustomerProfileSearchService(db);

        foreach (var query in new[] { "Nguyễn Văn Minh", "Minh", "nguyen van minh", "NGUYEN VAN MINH", "0912", "5678" })
        {
            var result = await search.SearchAsync(query);
            Assert.AreEqual(1, result.Results.Count, query);
            Assert.AreEqual(customer.CustomerId, result.Results[0].CustomerId, query);
        }
    }

    [TestMethod]
    public async Task Search_reports_short_or_missing_results_and_returns_new_or_renamed_profiles_immediately()
    {
        await using var db = CreateDb();
        var profiles = new CustomerProfileService(db);
        var search = new CustomerProfileSearchService(db);

        var tooShort = await search.SearchAsync("123");
        var notFound = await search.SearchAsync("Không tồn tại");
        Assert.IsNotNull(tooShort.Error);
        Assert.AreEqual(0, notFound.Results.Count);

        var customer = await profiles.FindOrCreateAsync("Tên ban đầu", "0912345678", null);
        await db.SaveChangesAsync();
        Assert.AreEqual(customer.CustomerId, (await search.SearchAsync("ban dau")).Results.Single().CustomerId);

        await profiles.FindOrCreateAsync("Tên mới", "0912345678", null);
        await db.SaveChangesAsync();
        Assert.AreEqual(0, (await search.SearchAsync("ban dau")).Results.Count);
        Assert.AreEqual(customer.CustomerId, (await search.SearchAsync("ten moi")).Results.Single().CustomerId);
    }

    [TestMethod]
    public async Task Search_returns_profile_totals_and_stays_under_one_second_with_five_thousand_profiles()
    {
        await using var db = CreateDb();
        var customers = Enumerable.Range(0, 5000)
            .Select(index => AddCustomer(index % 2 == 0 ? $"Nguyễn Văn Minh {index:D4}" : $"Trần Thị Lan {index:D4}", $"09{index:D8}"))
            .ToList();
        db.Customers.AddRange(customers); await db.SaveChangesAsync();
        db.Appointments.AddRange(customers.Select((customer, index) => new Appointment
        {
            CustomerId = customer.CustomerId,
            StylistId = 1,
            AppointmentDate = DateTime.Today,
            BookingReference = $"S{index:D6}",
            Status = index == 0 ? "Completed" : index == 2 ? "NoShow" : "Confirmed"
        }));
        await db.SaveChangesAsync();
        var search = new CustomerProfileSearchService(db);

        var stopwatch = Stopwatch.StartNew();
        var popularName = await search.SearchAsync("Minh");
        stopwatch.Stop();
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Tìm tên phổ biến mất {stopwatch.ElapsedMilliseconds}ms.");
        Assert.AreEqual(CustomerProfileSearchService.MaximumResults, popularName.Results.Count);

        var completedProfile = await search.SearchAsync("0900000000");
        Assert.AreEqual(1, completedProfile.Results.Single().VisitCount);

        stopwatch.Restart();
        var lastFourDigits = await search.SearchAsync("4999");
        stopwatch.Stop();
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Tìm 4 số cuối mất {stopwatch.ElapsedMilliseconds}ms.");
        Assert.AreEqual("0900004999", lastFourDigits.Results.Single().Phone);
    }

    private static Customer AddCustomer(string name, string phone) => new()
    {
        FullName = name,
        NormalizedSearchName = CustomerProfileService.NormalizeSearchName(name),
        Phone = phone
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
