using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Owner;
using SalonManagement.Controllers.Reviews;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class ServiceReviewsTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [TestMethod]
    public async Task Submit_ValidReview_SucceedsAndRecordsSubmittedAt()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(7));
        var clock = new FixedTimeProvider(now);
        var (appointment, token) = await SeedCompletedPaidAppointmentAsync(db, now.DateTime);

        var controller = new ServiceReviewsController(db, clock);
        var request = new SubmitServiceReviewRequest(token, null, 5, "Dịch vụ cắt tóc rất đẹp, thợ tư vấn nhiệt tình!");
        var result = await controller.Submit(request);

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var review = await db.ServiceReviews.SingleAsync();
        Assert.AreEqual(5, review.Rating);
        Assert.AreEqual("Dịch vụ cắt tóc rất đẹp, thợ tư vấn nhiệt tình!", review.Comment);
        Assert.IsNotNull(review.SubmittedAt);
    }

    [TestMethod]
    public async Task Submit_InvalidRating_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(7));
        var clock = new FixedTimeProvider(now);
        var (appointment, token) = await SeedCompletedPaidAppointmentAsync(db, now.DateTime);

        var controller = new ServiceReviewsController(db, clock);

        var low = await controller.Submit(new SubmitServiceReviewRequest(token, null, 0, null));
        var high = await controller.Submit(new SubmitServiceReviewRequest(token, null, 6, null));

        Assert.IsInstanceOfType<BadRequestObjectResult>(low);
        Assert.IsInstanceOfType<BadRequestObjectResult>(high);
    }

    [TestMethod]
    public async Task Submit_OverlongComment_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(7));
        var clock = new FixedTimeProvider(now);
        var (appointment, token) = await SeedCompletedPaidAppointmentAsync(db, now.DateTime);

        var controller = new ServiceReviewsController(db, clock);
        var overlong = new string('A', 501);
        var result = await controller.Submit(new SubmitServiceReviewRequest(token, null, 5, overlong));

        Assert.IsInstanceOfType<BadRequestObjectResult>(result);
    }

    [TestMethod]
    public async Task Submit_AlreadySubmittedReview_ReturnsConflict()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(7));
        var clock = new FixedTimeProvider(now);
        var (appointment, token) = await SeedCompletedPaidAppointmentAsync(db, now.DateTime);

        var review = await db.ServiceReviews.SingleAsync();
        review.SubmittedAt = now.DateTime.AddMinutes(-10);
        review.Rating = 4;
        await db.SaveChangesAsync();

        var controller = new ServiceReviewsController(db, clock);
        var result = await controller.Submit(new SubmitServiceReviewRequest(token, null, 5, "Đánh giá lại"));

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task Submit_ExpiredReview_ReturnsConflict()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(7));
        var clock = new FixedTimeProvider(now);
        var (appointment, token) = await SeedCompletedPaidAppointmentAsync(db, now.DateTime);

        var review = await db.ServiceReviews.SingleAsync();
        review.ExpiresAt = now.DateTime.AddDays(-1);
        await db.SaveChangesAsync();

        var controller = new ServiceReviewsController(db, clock);
        var result = await controller.Submit(new SubmitServiceReviewRequest(token, null, 5, "Hết hạn"));

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task Submit_UnpaidOrIncompleteAppointment_ReturnsConflict()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(7));
        var clock = new FixedTimeProvider(now);
        var (appointment, token) = await SeedCompletedPaidAppointmentAsync(db, now.DateTime);

        // Trường hợp chưa thanh toán
        appointment.Invoice!.Status = "PendingPayment";
        await db.SaveChangesAsync();

        var controller = new ServiceReviewsController(db, clock);
        var unpaidResult = await controller.Submit(new SubmitServiceReviewRequest(token, null, 5, "Chưa thanh toán"));
        Assert.IsInstanceOfType<ConflictObjectResult>(unpaidResult);

        // Trường hợp chưa hoàn tất dịch vụ
        appointment.Invoice.Status = "Paid";
        appointment.Status = "InProgress";
        await db.SaveChangesAsync();

        var inProgressResult = await controller.Submit(new SubmitServiceReviewRequest(token, null, 5, "Đang làm"));
        Assert.IsInstanceOfType<ConflictObjectResult>(inProgressResult);
    }

    [TestMethod]
    public async Task GetDetails_ByBookingReference_ReturnsExpectedDetails()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(7));
        var clock = new FixedTimeProvider(now);
        var (appointment, _) = await SeedCompletedPaidAppointmentAsync(db, now.DateTime);

        var controller = new ServiceReviewsController(db, clock);
        var actionResult = await controller.GetByReference("REV-1001");

        var ok = actionResult as OkObjectResult;
        Assert.IsNotNull(ok);
        var details = ok.Value as ServiceReviewDetailsResponse;
        Assert.IsNotNull(details);
        Assert.AreEqual("REV-1001", details.Reference);
        Assert.AreEqual("Thợ Hải", details.StylistName);
        Assert.IsTrue(details.Services.Contains("Cắt uốn cao cấp"));
    }

    [TestMethod]
    public async Task Owner_ReviewReport_CalculatesAveragesAndEnforcesThreshold()
    {
        await using var db = CreateDb();
        var stylist1 = new Stylist { FullName = "Nguyễn Văn A", Specialty = "Stylist Master", Phone = "0900000001", IsActive = true };
        var stylist2 = new Stylist { FullName = "Trần Thị B", Specialty = "Colorist", Phone = "0900000002", IsActive = true };
        db.Stylists.AddRange(stylist1, stylist2);
        await db.SaveChangesAsync();

        // Stylist 1: có 5 đánh giá (điểm: 5, 5, 4, 4, 4 -> Trung bình: 4.4)
        for (int i = 0; i < 5; i++)
        {
            var app = new Appointment
            {
                BookingReference = $"REV-ST1-{i}",
                StylistId = stylist1.StylistId,
                CustomerId = 1,
                AppointmentDate = DateTime.Today,
                Status = "Completed"
            };
            db.Appointments.Add(app);
            await db.SaveChangesAsync();
            db.ServiceReviews.Add(new ServiceReview
            {
                AppointmentId = app.AppointmentId,
                TokenHash = $"hash-1-{i}",
                SubmittedAt = DateTime.Now,
                Rating = i < 2 ? 5 : 4
            });
        }

        // Stylist 2: chỉ có 2 đánh giá (chưa đủ ngưỡng 5)
        for (int i = 0; i < 2; i++)
        {
            var app = new Appointment
            {
                BookingReference = $"REV-ST2-{i}",
                StylistId = stylist2.StylistId,
                CustomerId = 1,
                AppointmentDate = DateTime.Today,
                Status = "Completed"
            };
            db.Appointments.Add(app);
            await db.SaveChangesAsync();
            db.ServiceReviews.Add(new ServiceReview
            {
                AppointmentId = app.AppointmentId,
                TokenHash = $"hash-2-{i}",
                SubmittedAt = DateTime.Now,
                Rating = 5
            });
        }
        await db.SaveChangesAsync();

        var controller = new ServiceReviewReportsController(db);
        var action = await controller.Get();

        var ok = action as OkObjectResult;
        Assert.IsNotNull(ok);
        var rows = (ok.Value as IEnumerable<ServiceReviewReportRow>)?.ToList();
        Assert.IsNotNull(rows);

        var row1 = rows.Single(r => r.StylistId == stylist1.StylistId);
        Assert.AreEqual(5, row1.ReviewCount);
        Assert.IsTrue(row1.HasEnoughData);
        Assert.AreEqual(4.4m, row1.AverageRating);

        var row2 = rows.Single(r => r.StylistId == stylist2.StylistId);
        Assert.AreEqual(2, row2.ReviewCount);
        Assert.IsFalse(row2.HasEnoughData);
        Assert.IsNull(row2.AverageRating);
    }

    private static async Task<(Appointment Appointment, string Token)> SeedCompletedPaidAppointmentAsync(ApplicationDbContext db, DateTime now)
    {
        var customer = new Customer { FullName = "Khách Hàng", Phone = "0987654321" };
        var stylist = new Stylist { FullName = "Thợ Hải", Specialty = "Master Stylist", Phone = "0900000099", IsActive = true };
        var service = new Service { ServiceName = "Cắt uốn cao cấp", DurationMinutes = 60, Price = 300000m, IsActive = true };
        db.AddRange(customer, stylist, service);
        await db.SaveChangesAsync();

        var appointment = new Appointment
        {
            BookingReference = "REV-1001",
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            AppointmentDate = now.Date,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            Status = "Completed",
            ActualCompletedAt = now.AddMinutes(-30)
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        db.AppointmentServices.Add(new AppointmentService
        {
            AppointmentId = appointment.AppointmentId,
            ServiceId = service.ServiceId
        });

        var invoice = new Invoice
        {
            AppointmentId = appointment.AppointmentId,
            Subtotal = 300000m,
            DiscountAmount = 0m,
            TotalAmount = 300000m,
            Status = "Paid",
            CreatedAt = now.AddMinutes(-30)
        };
        db.Invoices.Add(invoice);
        appointment.Invoice = invoice;

        const string rawToken = "test-review-token-123456";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var review = new ServiceReview
        {
            AppointmentId = appointment.AppointmentId,
            TokenHash = tokenHash,
            CreatedAt = now.AddMinutes(-30),
            ExpiresAt = now.AddDays(7)
        };
        db.ServiceReviews.Add(review);
        await db.SaveChangesAsync();

        return (appointment, rawToken);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
