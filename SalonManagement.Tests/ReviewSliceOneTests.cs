using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using Xunit;

namespace SalonManagement.Tests;

public class ReviewSliceOneTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    [Fact]
    public void ModelValidation_RatingMustBeBetweenOneAndFive()
    {
        // Rating = 0 -> Invalid
        var modelZero = new StylistReviewViewModel { Reference = "ABCDEF12", Rating = 0, Comment = "Tốt" };
        var validationResults = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(modelZero, new ValidationContext(modelZero), validationResults, true));
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(modelZero.Rating)));

        // Rating = 6 -> Invalid
        var modelSix = new StylistReviewViewModel { Reference = "ABCDEF12", Rating = 6, Comment = "Tốt" };
        validationResults.Clear();
        Assert.False(Validator.TryValidateObject(modelSix, new ValidationContext(modelSix), validationResults, true));
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(modelSix.Rating)));

        // Rating = 1 -> Valid
        var modelOne = new StylistReviewViewModel { Reference = "ABCDEF12", Rating = 1 };
        validationResults.Clear();
        Assert.True(Validator.TryValidateObject(modelOne, new ValidationContext(modelOne), validationResults, true));

        // Rating = 5 -> Valid
        var modelFive = new StylistReviewViewModel { Reference = "ABCDEF12", Rating = 5 };
        validationResults.Clear();
        Assert.True(Validator.TryValidateObject(modelFive, new ValidationContext(modelFive), validationResults, true));
    }

    [Fact]
    public void ModelValidation_CommentLength_AllowsUpTo500Chars_Rejects501Chars()
    {
        // 500 characters -> Valid
        var model500 = new StylistReviewViewModel
        {
            Reference = "ABCDEF12",
            Rating = 5,
            Comment = new string('a', 500)
        };
        var validationResults = new List<ValidationResult>();
        Assert.True(Validator.TryValidateObject(model500, new ValidationContext(model500), validationResults, true));

        // 501 characters -> Invalid
        var model501 = new StylistReviewViewModel
        {
            Reference = "ABCDEF12",
            Rating = 5,
            Comment = new string('a', 501)
        };
        validationResults.Clear();
        Assert.False(Validator.TryValidateObject(model501, new ValidationContext(model501), validationResults, true));
        Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(model501.Comment)));
    }

    [Fact]
    public async Task SubmitReview_ValidRatingWithoutComment_Succeeds()
    {
        await using var db = CreateInMemoryDbContext();
        var (stylist, customer, appointment) = SeedClosedAppointment(db, "REV10001");

        var controller = new ReviewsController(db);
        var model = new StylistReviewViewModel
        {
            Reference = "REV10001",
            Rating = 5,
            Comment = null
        };

        var result = await controller.CreateByReference(model);
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Thanks", viewResult.ViewName);

        var savedReview = await db.StylistReviews.SingleAsync(r => r.AppointmentId == appointment.AppointmentId);
        Assert.Equal(5, savedReview.Rating);
        Assert.Null(savedReview.Comment);
        Assert.NotNull(savedReview.SubmittedAt);
    }

    [Fact]
    public async Task SubmitReview_VietnameseAccentedComment_SavedCorrectly()
    {
        await using var db = CreateInMemoryDbContext();
        var (stylist, customer, appointment) = SeedClosedAppointment(db, "REV10002");

        var controller = new ReviewsController(db);
        var vietnameseComment = "Thợ cắt tóc rất có tâm, đường kéo sắc sảo, gội đầu thư giãn tuyệt đối! Sẽ quay lại.";
        var model = new StylistReviewViewModel
        {
            Reference = "REV10002",
            Rating = 5,
            Comment = vietnameseComment
        };

        var result = await controller.CreateByReference(model);
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("Thanks", viewResult.ViewName);

        var savedReview = await db.StylistReviews.SingleAsync(r => r.AppointmentId == appointment.AppointmentId);
        Assert.Equal(5, savedReview.Rating);
        Assert.Equal(vietnameseComment, savedReview.Comment);
    }

    [Fact]
    public async Task SubmitReview_InvalidReference_ReturnsUnavailable()
    {
        await using var db = CreateInMemoryDbContext();
        var controller = new ReviewsController(db);

        // Reference length != 8
        var resultInvalid = await controller.CreateByReference("SHORT");
        var viewResult = Assert.IsType<ViewResult>(resultInvalid);
        Assert.Equal("Unavailable", viewResult.ViewName);

        // Non-existent 8-char reference
        var resultNotFound = await controller.CreateByReference("NOTEXIST");
        var viewResultNotFound = Assert.IsType<ViewResult>(resultNotFound);
        Assert.Equal("Unavailable", viewResultNotFound.ViewName);
    }

    [Fact]
    public async Task SubmitReview_IncompleteAppointment_CannotBeReviewed()
    {
        await using var db = CreateInMemoryDbContext();
        var customer = new Customer { CustomerId = 1, FullName = "Khách Hàng", Phone = "0900000001" };
        var stylist = new Stylist { StylistId = 1, FullName = "Thợ Chính", Phone = "0900000002" };
        var pendingAppointment = new Appointment
        {
            AppointmentId = 1,
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            Customer = customer,
            Stylist = stylist,
            BookingReference = "PENDING1",
            AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Status = "Pending"
        };
        db.Customers.Add(customer);
        db.Stylists.Add(stylist);
        db.Appointments.Add(pendingAppointment);
        await db.SaveChangesAsync();

        var controller = new ReviewsController(db);

        // GET review for pending appointment
        var getResult = await controller.CreateByReference("PENDING1");
        var getViewResult = Assert.IsType<ViewResult>(getResult);
        Assert.Equal("Unavailable", getViewResult.ViewName);

        // POST review for pending appointment
        var postResult = await controller.CreateByReference(new StylistReviewViewModel
        {
            Reference = "PENDING1",
            Rating = 5
        });
        var postViewResult = Assert.IsType<ViewResult>(postResult);
        Assert.Equal("Unavailable", postViewResult.ViewName);
    }

    [Fact]
    public async Task SubmitReview_DuplicateSubmission_PreventedAndShowsAlreadyReviewed()
    {
        await using var db = CreateInMemoryDbContext();
        var (stylist, customer, appointment) = SeedClosedAppointment(db, "REV10003");

        var controller = new ReviewsController(db);

        // 1st submission -> Success
        var firstSubmit = await controller.CreateByReference(new StylistReviewViewModel
        {
            Reference = "REV10003",
            Rating = 4,
            Comment = "Lần đầu"
        });
        Assert.Equal("Thanks", Assert.IsType<ViewResult>(firstSubmit).ViewName);

        // 2nd GET -> Shows AlreadyReviewed
        var secondGet = await controller.CreateByReference("REV10003");
        var alreadyReviewedView = Assert.IsType<ViewResult>(secondGet);
        Assert.Equal("AlreadyReviewed", alreadyReviewedView.ViewName);
        var viewModel = Assert.IsType<StylistReviewViewModel>(alreadyReviewedView.Model);
        Assert.Equal(4, viewModel.Rating);
        Assert.Equal("Lần đầu", viewModel.Comment);

        // 2nd POST -> Prevented, shows AlreadyReviewed
        var secondPost = await controller.CreateByReference(new StylistReviewViewModel
        {
            Reference = "REV10003",
            Rating = 1,
            Comment = "Cố tình ghi đè"
        });
        var secondPostView = Assert.IsType<ViewResult>(secondPost);
        Assert.Equal("AlreadyReviewed", secondPostView.ViewName);

        // Ensure database record was NOT overwritten
        var reviewInDb = await db.StylistReviews.SingleAsync(r => r.AppointmentId == appointment.AppointmentId);
        Assert.Equal(4, reviewInDb.Rating);
        Assert.Equal("Lần đầu", reviewInDb.Comment);
    }

    private static (Stylist Stylist, Customer Customer, Appointment Appointment) SeedClosedAppointment(ApplicationDbContext db, string reference)
    {
        var stylist = new Stylist { StylistId = Random.Shared.Next(1, 10000), FullName = "Nguyễn Văn Thợ", Phone = "0912345678" };
        var customer = new Customer { CustomerId = Random.Shared.Next(1, 10000), FullName = "Trần Thị Khách", Phone = "0987654321" };
        var appointment = new Appointment
        {
            AppointmentId = Random.Shared.Next(1, 10000),
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            Customer = customer,
            Stylist = stylist,
            BookingReference = reference,
            AppointmentDate = DateTime.Today,
            StartTime = TimeSpan.FromHours(14),
            EndTime = TimeSpan.FromHours(15),
            Status = "Closed"
        };
        db.Stylists.Add(stylist);
        db.Customers.Add(customer);
        db.Appointments.Add(appointment);
        db.SaveChanges();

        return (stylist, customer, appointment);
    }
}
