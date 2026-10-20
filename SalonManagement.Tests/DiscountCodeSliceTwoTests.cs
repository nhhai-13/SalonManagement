using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class DiscountCodeSliceTwoTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private static InvoicesController CreateController(ApplicationDbContext db)
    {
        var http = new DefaultHttpContext();
        return new InvoicesController(db, new NullEmailService(), NullLogger<InvoicesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new TestTempDataProvider())
        };
    }

    private sealed class NullEmailService : IEmailService
    {
        public Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword) => Task.CompletedTask;
        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink) => Task.CompletedTask;
        public Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode) => Task.CompletedTask;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static async Task<(Invoice invoice, DiscountCoupon activeCoupon)> SeedInvoiceAndCoupons(ApplicationDbContext db)
    {
        var customer = new Customer { FullName = "Khách hàng Test", Phone = "0901234567" };
        var stylist = new Stylist { FullName = "Thợ Test" };
        var service = new Service { ServiceName = "Cắt gội combo", Price = 450000m, DurationMinutes = 60, IsActive = true };

        db.Customers.Add(customer);
        db.Stylists.Add(stylist);
        db.Services.Add(service);
        await db.SaveChangesAsync();

        var today = VietnamTime.Today.Date;

        var appointment = new Appointment
        {
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            AppointmentDate = today,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Status = "Completed"
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var appointmentService = new AppointmentService
        {
            AppointmentId = appointment.AppointmentId,
            ServiceId = service.ServiceId,
            StylistId = stylist.StylistId,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            IsCompleted = true,
            CollectedAmount = service.Price
        };
        db.AppointmentServices.Add(appointmentService);
        await db.SaveChangesAsync();

        var invoice = new Invoice
        {
            AppointmentId = appointment.AppointmentId,
            Subtotal = 450000m,
            TotalAmount = 450000m,
            DiscountAmount = 0m,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow,
            Lines = new List<InvoiceLine>
            {
                new()
                {
                    AppointmentServiceId = appointmentService.AppointmentServiceId,
                    ServiceName = service.ServiceName,
                    UnitPrice = service.Price
                }
            }
        };
        db.Invoices.Add(invoice);

        var activeCoupon = new DiscountCoupon
        {
            Code = "SALE10",
            DiscountType = "Percent",
            Value = 10,
            StartsOn = today.AddDays(-1),
            EndsOn = today.AddDays(7),
            IsActive = true
        };
        db.DiscountCoupons.Add(activeCoupon);

        await db.SaveChangesAsync();
        return (invoice, activeCoupon);
    }

    [Fact]
    public async Task ApplyDiscount_CodeDoesNotExist_ReturnsErrorMessageAndPreservesAmounts()
    {
        using var db = CreateDbContext();
        var (invoice, _) = await SeedInvoiceAndCoupons(db);
        var controller = CreateController(db);

        var model = new ApplyDiscountViewModel { InvoiceId = invoice.InvoiceId, Code = "NONEXISTENT_CODE" };
        var result = await controller.ApplyDiscount(model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Mã giảm giá không tồn tại trên hệ thống.", controller.TempData["Error"]);

        var updatedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Equal(450000m, updatedInvoice.Subtotal);
        Assert.Equal(0m, updatedInvoice.DiscountAmount);
        Assert.Equal(450000m, updatedInvoice.TotalAmount);
        Assert.Null(updatedInvoice.DiscountCouponId);
    }

    [Fact]
    public async Task ApplyDiscount_CodeNotStartedYet_ReturnsErrorMessageAndPreservesAmounts()
    {
        using var db = CreateDbContext();
        var (invoice, _) = await SeedInvoiceAndCoupons(db);

        var today = VietnamTime.Today.Date;
        db.DiscountCoupons.Add(new DiscountCoupon
        {
            Code = "FUTURE10",
            DiscountType = "Percent",
            Value = 10,
            StartsOn = today.AddDays(2),
            EndsOn = today.AddDays(10),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var model = new ApplyDiscountViewModel { InvoiceId = invoice.InvoiceId, Code = "FUTURE10" };
        var result = await controller.ApplyDiscount(model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Mã giảm giá chưa đến ngày có hiệu lực.", controller.TempData["Error"]);

        var updatedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Equal(450000m, updatedInvoice.Subtotal);
        Assert.Equal(0m, updatedInvoice.DiscountAmount);
        Assert.Equal(450000m, updatedInvoice.TotalAmount);
    }

    [Fact]
    public async Task ApplyDiscount_CodeExpired_ReturnsErrorMessageAndPreservesAmounts()
    {
        using var db = CreateDbContext();
        var (invoice, _) = await SeedInvoiceAndCoupons(db);

        var today = VietnamTime.Today.Date;
        db.DiscountCoupons.Add(new DiscountCoupon
        {
            Code = "EXPIRED10",
            DiscountType = "Percent",
            Value = 10,
            StartsOn = today.AddDays(-10),
            EndsOn = today.AddDays(-1), // Expired yesterday
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var model = new ApplyDiscountViewModel { InvoiceId = invoice.InvoiceId, Code = "EXPIRED10" };
        var result = await controller.ApplyDiscount(model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Mã giảm giá đã hết hạn sử dụng.", controller.TempData["Error"]);

        var updatedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Equal(450000m, updatedInvoice.Subtotal);
        Assert.Equal(0m, updatedInvoice.DiscountAmount);
        Assert.Equal(450000m, updatedInvoice.TotalAmount);
    }

    [Fact]
    public async Task ApplyDiscount_ValidOnStartDateAndEndDate_Succeeds()
    {
        using var db = CreateDbContext();
        var (invoice, _) = await SeedInvoiceAndCoupons(db);

        var today = VietnamTime.Today.Date;
        // Coupon whose StartsOn is exactly today, and EndsOn is exactly today (valid until 23:59:59)
        db.DiscountCoupons.Add(new DiscountCoupon
        {
            Code = "TODAYONLY",
            DiscountType = "Percent",
            Value = 20,
            StartsOn = today,
            EndsOn = today,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var model = new ApplyDiscountViewModel { InvoiceId = invoice.InvoiceId, Code = "TODAYONLY" };
        var result = await controller.ApplyDiscount(model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Đã áp dụng mã giảm giá.", controller.TempData["Success"]);

        var updatedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Equal(450000m, updatedInvoice.Subtotal);
        Assert.Equal(90000m, updatedInvoice.DiscountAmount); // 20% of 450k
        Assert.Equal(360000m, updatedInvoice.TotalAmount);
        Assert.Equal("TODAYONLY", updatedInvoice.DiscountCodeSnapshot);
    }

    [Fact]
    public async Task ApplyDiscount_WhenAlreadyHasAppliedCode_RejectsWithSpecificMessage()
    {
        using var db = CreateDbContext();
        var (invoice, activeCoupon) = await SeedInvoiceAndCoupons(db);

        // Pre-apply SALE10
        invoice.DiscountCouponId = activeCoupon.DiscountCouponId;
        invoice.DiscountCoupon = activeCoupon;
        invoice.DiscountCodeSnapshot = activeCoupon.Code;
        invoice.DiscountTypeSnapshot = activeCoupon.DiscountType;
        invoice.DiscountValueSnapshot = activeCoupon.Value;
        invoice.DiscountAmount = 45000m;
        invoice.TotalAmount = 405000m;
        await db.SaveChangesAsync();

        var today = VietnamTime.Today.Date;
        db.DiscountCoupons.Add(new DiscountCoupon
        {
            Code = "ANOTHERCODE",
            DiscountType = "Fixed",
            Value = 50000,
            StartsOn = today,
            EndsOn = today.AddDays(5),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var model = new ApplyDiscountViewModel { InvoiceId = invoice.InvoiceId, Code = "ANOTHERCODE" };
        var result = await controller.ApplyDiscount(model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Phiếu này đã áp mã SALE10. Vui lòng gỡ mã hiện tại trước khi áp mã mới.", controller.TempData["Error"]);

        // Verify existing code and amounts were preserved
        var updatedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Equal("SALE10", updatedInvoice.DiscountCodeSnapshot);
        Assert.Equal(45000m, updatedInvoice.DiscountAmount);
        Assert.Equal(405000m, updatedInvoice.TotalAmount);
    }

    [Fact]
    public async Task RemoveDiscount_ClearsDiscountAndRestoresTotalAmount()
    {
        using var db = CreateDbContext();
        var (invoice, activeCoupon) = await SeedInvoiceAndCoupons(db);

        // Pre-apply SALE10
        invoice.DiscountCouponId = activeCoupon.DiscountCouponId;
        invoice.DiscountCoupon = activeCoupon;
        invoice.DiscountCodeSnapshot = activeCoupon.Code;
        invoice.DiscountTypeSnapshot = activeCoupon.DiscountType;
        invoice.DiscountValueSnapshot = activeCoupon.Value;
        invoice.DiscountAmount = 45000m;
        invoice.TotalAmount = 405000m;
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.RemoveDiscount(invoice.InvoiceId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Đã gỡ mã giảm giá.", controller.TempData["Success"]);

        var updatedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Null(updatedInvoice.DiscountCouponId);
        Assert.Null(updatedInvoice.DiscountCodeSnapshot);
        Assert.Equal(0m, updatedInvoice.DiscountAmount);
        Assert.Equal(450000m, updatedInvoice.TotalAmount);
        Assert.Equal(updatedInvoice.Subtotal, updatedInvoice.TotalAmount);
    }

    [Fact]
    public async Task RemoveDiscount_ThenApplyAnotherCode_Succeeds()
    {
        using var db = CreateDbContext();
        var (invoice, activeCoupon) = await SeedInvoiceAndCoupons(db);

        // Pre-apply SALE10
        invoice.DiscountCouponId = activeCoupon.DiscountCouponId;
        invoice.DiscountCoupon = activeCoupon;
        invoice.DiscountCodeSnapshot = activeCoupon.Code;
        invoice.DiscountTypeSnapshot = activeCoupon.DiscountType;
        invoice.DiscountValueSnapshot = activeCoupon.Value;
        invoice.DiscountAmount = 45000m;
        invoice.TotalAmount = 405000m;
        await db.SaveChangesAsync();

        var today = VietnamTime.Today.Date;
        db.DiscountCoupons.Add(new DiscountCoupon
        {
            Code = "FIXED50K",
            DiscountType = "Fixed",
            Value = 50000,
            StartsOn = today,
            EndsOn = today.AddDays(5),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        // 1. Remove current discount
        var removeResult = await controller.RemoveDiscount(invoice.InvoiceId);
        Assert.IsType<RedirectToActionResult>(removeResult);
        Assert.Equal("Đã gỡ mã giảm giá.", controller.TempData["Success"]);

        // 2. Apply new code FIXED50K
        var applyResult = await controller.ApplyDiscount(new ApplyDiscountViewModel
        {
            InvoiceId = invoice.InvoiceId,
            Code = "FIXED50K"
        });
        Assert.IsType<RedirectToActionResult>(applyResult);
        Assert.Equal("Đã áp dụng mã giảm giá.", controller.TempData["Success"]);

        var updatedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Equal("FIXED50K", updatedInvoice.DiscountCodeSnapshot);
        Assert.Equal(50000m, updatedInvoice.DiscountAmount);
        Assert.Equal(400000m, updatedInvoice.TotalAmount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ApplyDiscount_EmptyOrWhitespaceCode_Rejects(string? emptyCode)
    {
        using var db = CreateDbContext();
        var (invoice, _) = await SeedInvoiceAndCoupons(db);
        var controller = CreateController(db);

        var model = new ApplyDiscountViewModel { InvoiceId = invoice.InvoiceId, Code = emptyCode };
        var result = await controller.ApplyDiscount(model);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Vui lòng nhập mã giảm giá.", controller.TempData["Error"]);

        var updatedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Equal(450000m, updatedInvoice.Subtotal);
        Assert.Equal(0m, updatedInvoice.DiscountAmount);
        Assert.Equal(450000m, updatedInvoice.TotalAmount);
    }
}
