using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Owner;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public class ReviewSliceThreeTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    [Fact]
    public void Controller_HasOwnerOrAdminAuthorization()
    {
        var authAttr = typeof(StylistReviewsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);
        Assert.Contains(UserRoles.Owner, authAttr.Roles);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Index_UnderThreshold_HidesAverageAndShowsThresholdMessage(int reviewCount)
    {
        await using var db = CreateInMemoryDbContext();
        var stylist = new Stylist { StylistId = 1, FullName = "Thợ Thử Nghiệm", Phone = "0900000001", IsActive = true };
        var customer = new Customer { CustomerId = 1, FullName = "Khách Hàng", Phone = "0900000002" };
        db.Stylists.Add(stylist);
        db.Customers.Add(customer);

        for (int i = 1; i <= reviewCount; i++)
        {
            var appt = new Appointment
            {
                AppointmentId = i,
                CustomerId = 1,
                StylistId = 1,
                BookingReference = $"REF{i:00000}",
                AppointmentDate = DateTime.Today,
                StartTime = TimeSpan.FromHours(10),
                EndTime = TimeSpan.FromHours(11),
                Status = "Closed"
            };
            db.Appointments.Add(appt);
            db.StylistReviews.Add(new StylistReview
            {
                StylistReviewId = i,
                AppointmentId = i,
                CustomerId = 1,
                StylistId = 1,
                ReviewTokenHash = $"HASH{i}",
                RequestedAt = DateTime.UtcNow.AddDays(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(5),
                SubmittedAt = DateTime.UtcNow.AddDays(-1),
                Rating = 5,
                Comment = "Tốt"
            });
        }
        await db.SaveChangesAsync();

        var controller = new StylistReviewsController(db);
        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var summaries = Assert.IsAssignableFrom<IReadOnlyList<StylistReviewSummary>>(viewResult.Model);
        var summary = Assert.Single(summaries);

        Assert.Equal(reviewCount, summary.ReviewCount);
        Assert.Null(summary.AverageRating);
        Assert.False(summary.HasSufficientReviews);
        Assert.Equal($"Chưa đủ dữ liệu ({reviewCount}/5 lượt)", summary.RatingDisplay);
    }

    [Fact]
    public async Task Index_ExactlyFiveReviews_RevealsAverageRoundedToOneDecimal()
    {
        await using var db = CreateInMemoryDbContext();
        var stylist = new Stylist { StylistId = 1, FullName = "Thợ 5 Sao", Phone = "0900000001", IsActive = true };
        var customer = new Customer { CustomerId = 1, FullName = "Khách", Phone = "0900000002" };
        db.Stylists.Add(stylist);
        db.Customers.Add(customer);

        // Ratings: 5, 4, 5, 4, 5 -> Sum = 23 -> Average = 4.6
        var ratings = new[] { 5, 4, 5, 4, 5 };
        for (int i = 0; i < ratings.Length; i++)
        {
            var appt = new Appointment
            {
                AppointmentId = i + 1,
                CustomerId = 1,
                StylistId = 1,
                BookingReference = $"FIVE{i:0000}",
                AppointmentDate = DateTime.Today,
                StartTime = TimeSpan.FromHours(10),
                EndTime = TimeSpan.FromHours(11),
                Status = "Closed"
            };
            db.Appointments.Add(appt);
            db.StylistReviews.Add(new StylistReview
            {
                StylistReviewId = i + 1,
                AppointmentId = i + 1,
                CustomerId = 1,
                StylistId = 1,
                ReviewTokenHash = $"HASH_FIVE_{i}",
                RequestedAt = DateTime.UtcNow.AddDays(-2),
                ExpiresAt = DateTime.UtcNow.AddDays(5),
                SubmittedAt = DateTime.UtcNow.AddDays(-1),
                Rating = ratings[i]
            });
        }
        await db.SaveChangesAsync();

        var controller = new StylistReviewsController(db);
        var result = await controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var summaries = Assert.IsAssignableFrom<IReadOnlyList<StylistReviewSummary>>(viewResult.Model);
        var summary = Assert.Single(summaries);

        Assert.Equal(5, summary.ReviewCount);
        Assert.NotNull(summary.AverageRating);
        Assert.Equal(4.6, summary.AverageRating.Value);
        Assert.True(summary.HasSufficientReviews);
        Assert.Equal("4.6 / 5", summary.RatingDisplay);
    }

    [Fact]
    public async Task Details_ReturnsStylistReviews_OrderedNewestFirst()
    {
        await using var db = CreateInMemoryDbContext();
        var stylist = new Stylist { StylistId = 10, FullName = "Thợ Chuyên Nghiệp", Phone = "0900000010", IsActive = true };
        var customer = new Customer { CustomerId = 10, FullName = "Khách Hàng Thân Thiết", Phone = "0900000011" };
        db.Stylists.Add(stylist);
        db.Customers.Add(customer);

        var now = DateTime.UtcNow;
        var r1 = new StylistReview
        {
            StylistReviewId = 101,
            AppointmentId = 101,
            Appointment = new Appointment { AppointmentId = 101, CustomerId = 10, StylistId = 10, BookingReference = "REV10101", Status = "Closed" },
            CustomerId = 10,
            StylistId = 10,
            ReviewTokenHash = "H1",
            Rating = 4,
            Comment = "Đánh giá cũ hơn",
            SubmittedAt = now.AddDays(-3)
        };
        var r2 = new StylistReview
        {
            StylistReviewId = 102,
            AppointmentId = 102,
            Appointment = new Appointment { AppointmentId = 102, CustomerId = 10, StylistId = 10, BookingReference = "REV10102", Status = "Closed" },
            CustomerId = 10,
            StylistId = 10,
            ReviewTokenHash = "H2",
            Rating = 5,
            Comment = "Đánh giá mới nhất",
            SubmittedAt = now.AddHours(-1)
        };

        db.StylistReviews.AddRange(r1, r2);
        await db.SaveChangesAsync();

        var controller = new StylistReviewsController(db);
        var result = await controller.Details(10);

        var viewResult = Assert.IsType<ViewResult>(result);
        var details = Assert.IsType<StylistReviewDetailsViewModel>(viewResult.Model);

        Assert.Equal(10, details.StylistId);
        Assert.Equal("Thợ Chuyên Nghiệp", details.FullName);
        Assert.Equal(2, details.ReviewCount);
        Assert.Null(details.AverageRating); // < 5 reviews
        Assert.Equal("Chưa đủ dữ liệu (2/5 lượt)", details.RatingDisplay);

        // Verify order: newest first
        Assert.Equal(2, details.RecentReviews.Count);
        Assert.Equal("Đánh giá mới nhất", details.RecentReviews[0].Comment);
        Assert.Equal("Đánh giá cũ hơn", details.RecentReviews[1].Comment);
    }

    [Fact]
    public async Task Details_NonExistentStylist_ReturnsNotFound()
    {
        await using var db = CreateInMemoryDbContext();
        var controller = new StylistReviewsController(db);

        var result = await controller.Details(999);
        Assert.IsType<NotFoundResult>(result);
    }
}
