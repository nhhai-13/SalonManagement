using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using Xunit;

namespace SalonManagement.Tests;

public class ReviewSliceTwoTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    [Fact]
    public void ReviewInvitationToken_Lifecycle_Calculates7DayExpiryAndValidatesUsage()
    {
        var now = DateTime.UtcNow;
        var token = ReviewInvitationToken.Generate();
        var hash = ReviewInvitationToken.ComputeHash(token);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(64, hash.Length);

        var expiresAt = now.AddDays(ReviewInvitationToken.DefaultExpiryDays);
        Assert.True(ReviewInvitationToken.IsValid(now, expiresAt, isUsed: false));

        // Expired after 7 days
        var pastExpiry = expiresAt.AddSeconds(1);
        Assert.False(ReviewInvitationToken.IsValid(pastExpiry, expiresAt, isUsed: false));

        // Already used token
        Assert.False(ReviewInvitationToken.IsValid(now, expiresAt, isUsed: true));
    }

    [Fact]
    public async Task EvaluateToken_InvalidToken_ReturnsUnavailableWithInvalidMessage()
    {
        await using var db = CreateInMemoryDbContext();
        var controller = new ReviewsController(db);

        // Null token
        var resultNull = await controller.Evaluate(null);
        var viewNull = Assert.IsType<ViewResult>(resultNull);
        Assert.Equal("Unavailable", viewNull.ViewName);

        // Random non-existent token
        var resultNotFound = await controller.Evaluate("non_existent_token_12345");
        var viewNotFound = Assert.IsType<ViewResult>(resultNotFound);
        Assert.Equal("Unavailable", viewNotFound.ViewName);
    }

    [Fact]
    public async Task EvaluateToken_ExpiredToken_ReturnsUnavailableWithExpiredMessage()
    {
        await using var db = CreateInMemoryDbContext();
        var rawToken = ReviewInvitationToken.Generate();
        var tokenHash = ReviewInvitationToken.ComputeHash(rawToken);

        var customer = new Customer { CustomerId = 1, FullName = "Khách Hàng", Phone = "0900000001" };
        var stylist = new Stylist { StylistId = 1, FullName = "Thợ Chính", Phone = "0900000002" };
        var appointment = new Appointment
        {
            AppointmentId = 1,
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            Customer = customer,
            Stylist = stylist,
            BookingReference = "EXP00001",
            AppointmentDate = DateTime.Today.AddDays(-10),
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Status = "Closed"
        };
        var expiredReview = new StylistReview
        {
            StylistReviewId = 1,
            AppointmentId = appointment.AppointmentId,
            Appointment = appointment,
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            ReviewTokenHash = tokenHash,
            RequestedAt = DateTime.UtcNow.AddDays(-8),
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired!
        };

        db.Customers.Add(customer);
        db.Stylists.Add(stylist);
        db.Appointments.Add(appointment);
        db.StylistReviews.Add(expiredReview);
        await db.SaveChangesAsync();

        var controller = new ReviewsController(db);
        var result = await controller.Evaluate(rawToken);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Unavailable", viewResult.ViewName);
    }

    [Fact]
    public async Task EvaluateToken_AlreadyUsedToken_ReturnsAlreadyReviewed()
    {
        await using var db = CreateInMemoryDbContext();
        var rawToken = ReviewInvitationToken.Generate();
        var tokenHash = ReviewInvitationToken.ComputeHash(rawToken);

        var customer = new Customer { CustomerId = 2, FullName = "Khách Hàng 2", Phone = "0900000003" };
        var stylist = new Stylist { StylistId = 2, FullName = "Thợ Chính 2", Phone = "0900000004" };
        var appointment = new Appointment
        {
            AppointmentId = 2,
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            Customer = customer,
            Stylist = stylist,
            BookingReference = "USED0002",
            AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Status = "Closed"
        };
        var usedReview = new StylistReview
        {
            StylistReviewId = 2,
            AppointmentId = appointment.AppointmentId,
            Appointment = appointment,
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            ReviewTokenHash = tokenHash,
            RequestedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(6),
            SubmittedAt = DateTime.UtcNow.AddHours(-2),
            Rating = 5,
            Comment = "Đã đánh giá rồi"
        };

        db.Customers.Add(customer);
        db.Stylists.Add(stylist);
        db.Appointments.Add(appointment);
        db.StylistReviews.Add(usedReview);
        await db.SaveChangesAsync();

        var controller = new ReviewsController(db);
        var result = await controller.Evaluate(rawToken);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("AlreadyReviewed", viewResult.ViewName);
        var model = Assert.IsType<StylistReviewViewModel>(viewResult.Model);
        Assert.Equal(5, model.Rating);
        Assert.Equal("Đã đánh giá rồi", model.Comment);
    }

    [Fact]
    public async Task SubmitReviewWithToken_SingleUseCheck_FirstSucceedsSecondFails()
    {
        await using var db = CreateInMemoryDbContext();
        var rawToken = ReviewInvitationToken.Generate();
        var tokenHash = ReviewInvitationToken.ComputeHash(rawToken);

        var customer = new Customer { CustomerId = 3, FullName = "Khách Hàng 3", Phone = "0900000005" };
        var stylist = new Stylist { StylistId = 3, FullName = "Thợ Chính 3", Phone = "0900000006" };
        var appointment = new Appointment
        {
            AppointmentId = 3,
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            Customer = customer,
            Stylist = stylist,
            BookingReference = "TOKN0003",
            AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(14),
            EndTime = TimeSpan.FromHours(15),
            Status = "Closed"
        };
        var validReview = new StylistReview
        {
            StylistReviewId = 3,
            AppointmentId = appointment.AppointmentId,
            Appointment = appointment,
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            ReviewTokenHash = tokenHash,
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        db.Customers.Add(customer);
        db.Stylists.Add(stylist);
        db.Appointments.Add(appointment);
        db.StylistReviews.Add(validReview);
        await db.SaveChangesAsync();

        var controller = new ReviewsController(db);

        // 1st POST with token -> Success
        var firstResult = await controller.Create(new StylistReviewViewModel
        {
            Token = rawToken,
            Rating = 5,
            Comment = "Rất hài lòng!"
        });
        var firstView = Assert.IsType<ViewResult>(firstResult);
        Assert.Equal("Thanks", firstView.ViewName);

        // 2nd POST with same token -> Prevented, shows AlreadyReviewed
        var secondResult = await controller.Create(new StylistReviewViewModel
        {
            Token = rawToken,
            Rating = 2,
            Comment = "Muốn sửa lại"
        });
        var secondView = Assert.IsType<ViewResult>(secondResult);
        Assert.Equal("AlreadyReviewed", secondView.ViewName);

        // Verify record in DB keeps 1st submission
        var updatedReview = await db.StylistReviews.SingleAsync(r => r.AppointmentId == 3);
        Assert.Equal(5, updatedReview.Rating);
        Assert.Equal("Rất hài lòng!", updatedReview.Comment);
        Assert.NotNull(updatedReview.SubmittedAt);
    }

    [Fact]
    public async Task ReviewToken_CreatedWithExact7DayDuration()
    {
        await using var db = CreateInMemoryDbContext();
        var rawToken = ReviewInvitationToken.Generate();
        var tokenHash = ReviewInvitationToken.ComputeHash(rawToken);
        var requestedAt = DateTime.UtcNow;
        var expiresAt = requestedAt.AddDays(7);

        var review = new StylistReview
        {
            StylistReviewId = 10,
            AppointmentId = 10,
            CustomerId = 10,
            StylistId = 10,
            ReviewTokenHash = tokenHash,
            RequestedAt = requestedAt,
            ExpiresAt = expiresAt
        };

        db.StylistReviews.Add(review);
        await db.SaveChangesAsync();

        var saved = await db.StylistReviews.SingleAsync(r => r.StylistReviewId == 10);
        Assert.Equal(7, (saved.ExpiresAt - saved.RequestedAt).Days);
        Assert.Equal(requestedAt.Date.AddDays(7), saved.ExpiresAt.Date);
    }
}
