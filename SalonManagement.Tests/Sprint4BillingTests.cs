using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using SalonManagement.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Controllers.Admin;
using SalonManagement.Controllers.Owner;
using SalonManagement.Models;
using SalonManagement.Controllers;
using SalonManagement.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace SalonManagement.Tests;

[TestClass]
public class Sprint4BillingTests
{
    [TestMethod]
    public void PercentCouponCalculatesFromCurrentSubtotal()
    {
        Assert.AreEqual(45_000m, DiscountCalculator.Calculate(450_000m, "Percent", 10m));
    }

    [TestMethod]
    public void FixedCouponCannotReduceInvoiceBelowZero()
    {
        Assert.AreEqual(75_000m, DiscountCalculator.Calculate(75_000m, "Fixed", 200_000m));
    }

    [TestMethod]
    public void EmptyInvoiceHasNoDiscount()
    {
        Assert.AreEqual(0m, DiscountCalculator.Calculate(0m, "Percent", 10m));
    }

    [TestMethod]
    public void PercentOverOneHundredIsRejected()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => DiscountCalculator.Calculate(10m, "Percent", 101m));
    }

    [TestMethod]
    public async Task StagingEmailIsRecordedWithoutAttemptingSmtpDelivery()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SmtpSettings:Server"] = "127.0.0.1",
            ["SmtpSettings:SenderEmail"] = "staging@salon.local"
        }).Build();
        var email = new EmailService(NullLogger<EmailService>.Instance, config, new StagingEnvironment(), db);

        await email.SendAppointmentChangeEmailAsync("demo.customer@salon.local", "Thợ mẫu", new DateTime(2026, 10, 14), TimeSpan.FromHours(10));

        var message = await db.StagingEmailMessages.SingleAsync();
        Assert.AreEqual("appointment-change", message.Template);
        Assert.AreEqual("demo.customer@salon.local", message.Recipient);
    }

    [TestMethod]
    public void Sprint4ManagementPagesHaveExplicitRoleRestrictions()
    {
        AssertRole<DiscountCouponsController>(UserRoles.Owner);
        AssertRole<OwnerReportsController>(UserRoles.Owner);
        AssertRole<PermissionAuditController>(UserRoles.Admin);
        AssertRole<Sprint4StagingController>(UserRoles.Admin);
    }

    [TestMethod]
    public async Task Sprint4ManagementEndpointsEnforceRolesThroughAspNetAuthorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        await AssertEndpointRole<DiscountCouponsController>(authorization, UserRoles.Owner);
        await AssertEndpointRole<OwnerReportsController>(authorization, UserRoles.Owner);
        await AssertEndpointRole<PermissionAuditController>(authorization, UserRoles.Admin);
        await AssertEndpointRole<Sprint4StagingController>(authorization, UserRoles.Admin);
    }

    [TestMethod]
    public async Task ExistingSqliteStagingDatabaseCanBeUpgradedWithoutDroppingInvoiceRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Invoices (InvoiceId INTEGER PRIMARY KEY, TotalAmount NUMERIC NOT NULL);");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Invoices (InvoiceId, TotalAmount) VALUES (1, 125000);");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Appointments (AppointmentId INTEGER PRIMARY KEY);");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Customers (CustomerId INTEGER PRIMARY KEY);");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Stylists (StylistId INTEGER PRIMARY KEY);");

        await SqliteSprint4SchemaUpgrade.EnsureAsync(db);

        Assert.AreEqual(125000m, await db.Database.SqlQueryRaw<decimal>("SELECT TotalAmount AS Value FROM Invoices WHERE InvoiceId = 1").SingleAsync());
        var columnNames = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(\"Invoices\")";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columnNames.Add(reader.GetString(1));
        }
        CollectionAssert.IsSubsetOf(new[] { "DiscountCodeSnapshot", "DiscountCouponId", "DiscountTypeSnapshot", "DiscountValueSnapshot" }, columnNames);
        Assert.IsTrue(await TableExists(db, "StylistReviews"));
        Assert.IsTrue(await TableExists(db, "DiscountCoupons"));
    }

    [TestMethod]
    public async Task ReviewLinkCanBeUsedOnceAndOnlyForClosedAppointments()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        var customer = new Customer { FullName = "Khách thử", Phone = "0900000001" };
        var stylist = new Stylist { FullName = "Thợ thử", Phone = "0900000002" };
        var appointment = new Appointment { Customer = customer, Stylist = stylist, AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), BookingReference = "REVIEW-TEST", Status = "Closed" };
        const string token = "secure-one-time-review-token";
        db.StylistReviews.Add(new StylistReview
        {
            Appointment = appointment, Customer = customer, Stylist = stylist,
            ReviewTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync();
        var controller = new ReviewsController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        var get = await controller.Create(token) as ViewResult;
        Assert.IsNotNull(get?.Model as StylistReviewViewModel);
        var posted = await controller.Create(new StylistReviewViewModel { Token = token, Rating = 5, Comment = "Tốt" });
        Assert.AreEqual("Thanks", (posted as ViewResult)?.ViewName);
        Assert.AreEqual(5, await db.StylistReviews.Select(item => item.Rating).SingleAsync());
        Assert.AreEqual("Unavailable", (await controller.Create(token) as ViewResult)?.ViewName);
    }

    private static void AssertRole<TController>(string expectedRole)
    {
        var attribute = typeof(TController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        Assert.AreEqual(expectedRole, attribute.Roles);
    }

    private static async Task AssertEndpointRole<TController>(IAuthorizationService authorization, string expectedRole)
    {
        var controller = typeof(TController);
        var controllerAttributes = controller.GetCustomAttributes(true);
        foreach (var method in controller.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                     .Where(method => method.DeclaringType == controller && !method.IsSpecialName))
        {
            var attributes = controllerAttributes.Concat(method.GetCustomAttributes(true)).ToArray();
            if (attributes.OfType<AllowAnonymousAttribute>().Any()) continue;
            var authorize = attributes.OfType<AuthorizeAttribute>().ToArray();
            Assert.IsTrue(authorize.Length > 0, $"{controller.Name}.{method.Name} must explicitly authorize access.");
            var roleSets = authorize.Select(attribute => (attribute.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal)).ToArray();
            var target = Principal(expectedRole);
            Assert.IsTrue((await authorization.AuthorizeAsync(target, resource: null, BuildPolicy(authorize))).Succeeded,
                $"{controller.Name}.{method.Name} must permit {expectedRole}.");
            foreach (var otherRole in new[] { UserRoles.Customer, UserRoles.Stylist, UserRoles.Receptionist, UserRoles.Admin, "Anonymous" }
                         .Where(role => role != expectedRole && roleSets.Any(set => set.Count > 0 && !set.Contains(role))))
            {
                var principal = otherRole == "Anonymous" ? new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()) : Principal(otherRole);
                Assert.IsFalse((await authorization.AuthorizeAsync(principal, resource: null, BuildPolicy(authorize))).Succeeded,
                    $"{controller.Name}.{method.Name} must deny {otherRole}.");
            }
        }
    }

    private static Microsoft.AspNetCore.Authorization.AuthorizationPolicy BuildPolicy(AuthorizeAttribute[] attributes)
    {
        var builder = new AuthorizationPolicyBuilder();
        foreach (var attribute in attributes)
        {
            if (string.IsNullOrWhiteSpace(attribute.Roles)) builder.RequireAuthenticatedUser();
            else builder.RequireRole(attribute.Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }
        return builder.Build();
    }

    private static System.Security.Claims.ClaimsPrincipal Principal(string role) =>
        new(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role) }, "test"));

    private static async Task<bool> TableExists(ApplicationDbContext db, string table) =>
        await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name={0}", table).SingleAsync() > 0;

    private sealed class StagingEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SalonManagement.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Staging";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
