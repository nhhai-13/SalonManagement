using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public sealed class ReceptionCustomerDirectoryTests
{
    [Fact]
    public async Task Prefix_search_returns_match_from_five_thousand_sqlite_profiles_in_under_one_second()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        db.Customers.AddRange(Enumerable.Range(0, 5_000).Select(index => new Customer
        {
            FullName = $"Khách {index:D4}",
            Phone = $"09{index:D8}"
        }));
        await db.SaveChangesAsync();

        var controller = new ReceptionCustomerDirectoryController(db);
        _ = await controller.Search("Khách 4999", CancellationToken.None); // Warm up EF and SQLite query compilation.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = Assert.IsType<OkObjectResult>(await controller.Search("Khách 4999", CancellationToken.None));
        stopwatch.Stop();

        var matches = Assert.IsAssignableFrom<IReadOnlyList<CustomerProfileResult>>(result.Value);
        var match = Assert.Single(matches);
        Assert.Equal("Khách 4999", match.FullName);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Customer search took {stopwatch.Elapsed.TotalMilliseconds:0} ms over 5,000 SQLite profiles.");
    }
}
