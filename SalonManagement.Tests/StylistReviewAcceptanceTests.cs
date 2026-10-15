using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Owner;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using Xunit;

namespace SalonManagement.Tests;

public class StylistReviewAcceptanceTests
{
    [Fact]
    public async Task OwnerReviewSummary_HidesAverageBeforeFiveRatings_AndShowsItAtFive()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        var underThreshold = new Stylist { StylistId = 1, FullName = "Thợ A", Phone = "0900000001" };
        var eligible = new Stylist { StylistId = 2, FullName = "Thợ B", Phone = "0900000002" };
        db.Stylists.AddRange(underThreshold, eligible);
        db.StylistReviews.AddRange(Enumerable.Range(1, 4).Select(index => Review(underThreshold, index, 5))
            .Concat(new[] { 5, 4, 5, 4, 5 }.Select((rating, index) => Review(eligible, index + 10, rating))));
        await db.SaveChangesAsync();

        var controller = new StylistReviewsController(db);
        var result = Assert.IsType<ViewResult>(await controller.Index());
        var summaries = Assert.IsAssignableFrom<IReadOnlyList<StylistReviewSummary>>(result.Model);

        Assert.Equal(4, summaries.Single(item => item.StylistId == 1).ReviewCount);
        Assert.Null(summaries.Single(item => item.StylistId == 1).AverageRating);
        Assert.Equal(5, summaries.Single(item => item.StylistId == 2).ReviewCount);
        Assert.Equal(4.6, summaries.Single(item => item.StylistId == 2).AverageRating);
    }

    [Fact]
    public void ReviewForm_RequiresOneToFiveStars_AndLimitsCommentToFiveHundredCharacters()
    {
        var model = new StylistReviewViewModel { Token = "token", Rating = 5, Comment = new string('x', 501) };
        var validation = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(model, new ValidationContext(model), validation, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(validation, item => item.MemberNames.Contains(nameof(model.Comment)));

        model.Comment = null;
        model.Rating = 0;
        validation.Clear();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), validation, validateAllProperties: true));
        Assert.Contains(validation, item => item.MemberNames.Contains(nameof(model.Rating)));
    }

    private static StylistReview Review(Stylist stylist, int seed, int rating)
    {
        var customer = new Customer { CustomerId = seed, FullName = $"Khách {seed}", Phone = $"09{seed:00000000}" };
        var appointment = new Appointment
        {
            AppointmentId = seed,
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            Customer = customer,
            Stylist = stylist,
            BookingReference = $"REV{seed}",
            AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Status = "Closed"
        };
        return new StylistReview
        {
            StylistReviewId = seed,
            AppointmentId = appointment.AppointmentId,
            Appointment = appointment,
            CustomerId = customer.CustomerId,
            Customer = customer,
            StylistId = stylist.StylistId,
            Stylist = stylist,
            ReviewTokenHash = $"HASH{seed}",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RequestedAt = DateTime.UtcNow.AddDays(-1),
            SubmittedAt = DateTime.UtcNow,
            Rating = rating
        };
    }
}
