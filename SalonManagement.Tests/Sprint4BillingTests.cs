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
