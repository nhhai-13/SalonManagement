using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Controllers.Owner;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using SalonManagement.Models.ViewModels.Owner;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class DiscountCodeSliceThreeTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private static InvoicesController CreateInvoicesController(ApplicationDbContext db)
    {
        var http = new DefaultHttpContext();
        return new InvoicesController(db, new NullEmailService(), NullLogger<InvoicesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new TestTempDataProvider())
        };
    }

    private static DiscountCouponsController CreateCouponsController(ApplicationDbContext db)
    {
        var http = new DefaultHttpContext();
        return new DiscountCouponsController(db)
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

    private static async Task<(Invoice invoice, Service service1, Service service2, DiscountCoupon coupon)> SeedSetup(ApplicationDbContext db)
    {
        var customer = new Customer { FullName = "Khách hàng Sprint 4", Phone = "0987654321" };
        var stylist = new Stylist { FullName = "Thợ chính Sprint 4" };
        var service1 = new Service { ServiceName = "Dịch vụ A", Price = 100000m, DurationMinutes = 30, IsActive = true };
        var service2 = new Service { ServiceName = "Dịch vụ B", Price = 200000m, DurationMinutes = 45, IsActive = true };

        db.Customers.Add(customer);
        db.Stylists.Add(stylist);
        db.Services.AddRange(service1, service2);
        await db.SaveChangesAsync();

        var today = VietnamTime.Today.Date;
        var appointment = new Appointment
        {
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            AppointmentDate = today,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(10),
            Status = "Completed"
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var apptService1 = new AppointmentService
        {
            AppointmentId = appointment.AppointmentId,
            ServiceId = service1.ServiceId,
            StylistId = stylist.StylistId,
            Price = service1.Price,
            DurationMinutes = service1.DurationMinutes,
            IsCompleted = true,
            CollectedAmount = service1.Price
        };
        db.AppointmentServices.Add(apptService1);
        await db.SaveChangesAsync();

        var coupon = new DiscountCoupon
        {
            Code = "DISCOUNT10",
            DiscountType = "Percent",
            Value = 10,
            StartsOn = today.AddDays(-2),
            EndsOn = today.AddDays(10),
            IsActive = true
        };
        db.DiscountCoupons.Add(coupon);
        await db.SaveChangesAsync();

        var invoice = new Invoice
        {
            AppointmentId = appointment.AppointmentId,
            Subtotal = 100000m,
            DiscountCouponId = coupon.DiscountCouponId,
            DiscountCoupon = coupon,
            DiscountCodeSnapshot = coupon.Code,
            DiscountTypeSnapshot = coupon.DiscountType,
            DiscountValueSnapshot = coupon.Value,
            DiscountAmount = 10000m,
            TotalAmount = 90000m,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow,
            Lines = new List<InvoiceLine>
            {
                new()
                {
                    AppointmentServiceId = apptService1.AppointmentServiceId,
                    ServiceName = service1.ServiceName,
                    UnitPrice = service1.Price
                }
            }
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        return (invoice, service1, service2, coupon);
    }

    [Fact]
    public async Task AddService_ToInvoiceWithTenPercentDiscount_RecalculatesDiscountAndTotal()
    {
        using var db = CreateDbContext();
        var (invoice, _, service2, _) = await SeedSetup(db);
        var controller = CreateInvoicesController(db);

        // Add service B (200,000 đ) to existing 100,000 đ invoice with 10% discount
        var result = await controller.AddService(invoice.InvoiceId, new AddInvoiceServiceViewModel { ServiceId = service2.ServiceId });
        Assert.NotNull(result);

        var updatedInvoice = await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.InvoiceId == invoice.InvoiceId);
        Assert.NotNull(updatedInvoice);
        Assert.Equal(2, updatedInvoice.Lines.Count);
        Assert.Equal(300000m, updatedInvoice.Subtotal); // 100k + 200k = 300k
        Assert.Equal(30000m, updatedInvoice.DiscountAmount); // 10% of 300k = 30k
        Assert.Equal(270000m, updatedInvoice.TotalAmount); // 300k - 30k = 270k
    }

    [Fact]
    public async Task RemoveService_FromInvoiceWithDiscount_RecalculatesDiscountAndTotal()
    {
        using var db = CreateDbContext();
        var (invoice, _, service2, _) = await SeedSetup(db);
        var controller = CreateInvoicesController(db);

        // First add service2 so we have 2 lines (subtotal = 300,000 đ)
        await controller.AddService(invoice.InvoiceId, new AddInvoiceServiceViewModel { ServiceId = service2.ServiceId });

        var invoiceWithTwoLines = await db.Invoices.Include(i => i.Lines).FirstAsync(i => i.InvoiceId == invoice.InvoiceId);
        Assert.Equal(300000m, invoiceWithTwoLines.Subtotal);
        var lineToRemove = invoiceWithTwoLines.Lines.First(l => l.ServiceName == service2.ServiceName);

        // Now remove service2
        var removeResult = await controller.RemoveService(invoice.InvoiceId, lineToRemove.InvoiceLineId);
        Assert.NotNull(removeResult);

        var updatedInvoice = await db.Invoices.Include(i => i.Lines).FirstAsync(i => i.InvoiceId == invoice.InvoiceId);
        Assert.Single(updatedInvoice.Lines);
        Assert.Equal(100000m, updatedInvoice.Subtotal); // Back to 100k
        Assert.Equal(10000m, updatedInvoice.DiscountAmount); // 10% of 100k = 10k
        Assert.Equal(90000m, updatedInvoice.TotalAmount); // 100k - 10k = 90k
    }

    [Fact]
    public async Task UpdateCoupon_AfterInvoiceIsClosedOrPaid_IsBlockedAndPreservesInvoiceSnapshot()
    {
        using var db = CreateDbContext();
        var (invoice, _, _, coupon) = await SeedSetup(db);

        // Pay and close the invoice
        invoice.Status = "Paid";
        invoice.ClosedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var couponsController = CreateCouponsController(db);

        // Attempt to update coupon from 10% to 50%
        var updateModel = new DiscountCouponViewModel
        {
            Code = coupon.Code,
            DiscountType = "Percent",
            Value = 50,
            StartsOn = coupon.StartsOn,
            EndsOn = coupon.EndsOn
        };
        var updateResult = await couponsController.Update(coupon.DiscountCouponId, updateModel);

        var redirect = Assert.IsType<RedirectToActionResult>(updateResult);
        Assert.Equal(nameof(DiscountCouponsController.Index), redirect.ActionName);
        Assert.Equal("Mã đã được áp vào phiếu; không thể sửa để giữ nguyên lịch sử đã chốt.", couponsController.TempData["Error"]);

        // Verify the invoice's snapshots and amounts are completely preserved
        var closedInvoice = await db.Invoices.FindAsync(invoice.InvoiceId);
        Assert.NotNull(closedInvoice);
        Assert.Equal(100000m, closedInvoice.Subtotal);
        Assert.Equal(10000m, closedInvoice.DiscountAmount);
        Assert.Equal(90000m, closedInvoice.TotalAmount);
        Assert.Equal("DISCOUNT10", closedInvoice.DiscountCodeSnapshot);
        Assert.Equal(10m, closedInvoice.DiscountValueSnapshot);
        Assert.Equal("Percent", closedInvoice.DiscountTypeSnapshot);
    }

    [Fact]
    public async Task DeleteCoupon_WhenUsedOnInvoice_IsBlocked()
    {
        using var db = CreateDbContext();
        var (invoice, _, _, coupon) = await SeedSetup(db);
        var couponsController = CreateCouponsController(db);

        // Coupon has been used on an invoice, attempt delete
        var deleteResult = await couponsController.Delete(coupon.DiscountCouponId);
        var redirect = Assert.IsType<RedirectToActionResult>(deleteResult);
        Assert.Equal(nameof(DiscountCouponsController.Index), redirect.ActionName);
        Assert.Equal("Không thể xóa mã giảm giá đã được dùng trên phiếu.", couponsController.TempData["Error"]);

        // Verify coupon still exists
        var existingCoupon = await db.DiscountCoupons.FindAsync(coupon.DiscountCouponId);
        Assert.NotNull(existingCoupon);
    }

    [Fact]
    public async Task DailyReport_RevenueEqualsSumOfPaidInvoicesNetTotal()
    {
        using var db = CreateDbContext();
        var today = VietnamTime.Today.Date;
        var (startUtc, endUtc) = (DateTime.SpecifyKind(today.AddHours(-7), DateTimeKind.Utc), DateTime.SpecifyKind(today.AddDays(1).AddHours(-7), DateTimeKind.Utc));

        var customer = new Customer { FullName = "Khách hàng Báo cáo", Phone = "0911222333" };
        var stylist = new Stylist { FullName = "Thợ Báo cáo" };
        var service1 = new Service { ServiceName = "Uốn tóc", Price = 500000m, DurationMinutes = 90, IsActive = true };
        var service2 = new Service { ServiceName = "Cắt tóc", Price = 300000m, DurationMinutes = 30, IsActive = true };

        db.Customers.Add(customer);
        db.Stylists.Add(stylist);
        db.Services.AddRange(service1, service2);
        await db.SaveChangesAsync();

        // 1. Paid Invoice 1: Original 500k, Discount 50k, Net = 450k
        var appt1 = new Appointment { CustomerId = customer.CustomerId, StylistId = stylist.StylistId, AppointmentDate = today, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10), Status = "Closed", ClosedAt = DateTime.UtcNow };
        db.Appointments.Add(appt1);
        await db.SaveChangesAsync();

        var inv1 = new Invoice
        {
            AppointmentId = appt1.AppointmentId,
            Subtotal = 500000m,
            DiscountAmount = 50000m,
            TotalAmount = 450000m,
            Status = "Paid",
            ClosedAt = DateTime.UtcNow,
            Payments = new List<Payment>
            {
                new() { Amount = 450000m, PaymentStatus = "Paid", PaidAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, PaymentMethod = "Cash" }
            }
        };
        db.Invoices.Add(inv1);

        // 2. Paid Invoice 2: Original 300k, Discount 30k (10%), Net = 270k
        var appt2 = new Appointment { CustomerId = customer.CustomerId, StylistId = stylist.StylistId, AppointmentDate = today, StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(12), Status = "Closed", ClosedAt = DateTime.UtcNow };
        db.Appointments.Add(appt2);
        await db.SaveChangesAsync();

        var inv2 = new Invoice
        {
            AppointmentId = appt2.AppointmentId,
            Subtotal = 300000m,
            DiscountAmount = 30000m,
            TotalAmount = 270000m,
            Status = "Paid",
            ClosedAt = DateTime.UtcNow,
            Payments = new List<Payment>
            {
                new() { Amount = 270000m, PaymentStatus = "Paid", PaidAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, PaymentMethod = "BankTransfer", TransactionCode = "TX123" }
            }
        };
        db.Invoices.Add(inv2);

        // 3. Unpaid Invoice 3: Original 200k (Not paid, should not count)
        var appt3 = new Appointment { CustomerId = customer.CustomerId, StylistId = stylist.StylistId, AppointmentDate = today, StartTime = TimeSpan.FromHours(14), EndTime = TimeSpan.FromHours(15), Status = "Completed" };
        db.Appointments.Add(appt3);
        await db.SaveChangesAsync();

        var inv3 = new Invoice { AppointmentId = appt3.AppointmentId, Subtotal = 200000m, DiscountAmount = 0m, TotalAmount = 200000m, Status = "Unpaid" };
        db.Invoices.Add(inv3);

        await db.SaveChangesAsync();

        var reportsController = new OwnerReportsController(db);
        var todayResult = await reportsController.Today();
        var viewResult = Assert.IsType<ViewResult>(todayResult);
        var model = Assert.IsType<OwnerTodayDashboardViewModel>(viewResult.Model);

        // Daily collected revenue must equal the sum of NET revenue (Thực thu = 450k + 270k = 720k), NOT subtotal (800k)
        Assert.Equal(720000m, model.CollectedRevenue);
        Assert.NotEqual(800000m, model.CollectedRevenue);
    }
}
