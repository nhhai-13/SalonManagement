using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Owner;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Tests;

[TestClass]
public sealed class OwnerReportsTests
{
    [TestMethod]
    public async Task PeakHoursReport_InvalidRange_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var controller = new PeakHoursReportController(db);

        // Ngày kết thúc trước ngày bắt đầu
        var invalidDates = await controller.Get(new DateTime(2026, 10, 10), new DateTime(2026, 10, 5));
        Assert.IsInstanceOfType<BadRequestObjectResult>(invalidDates.Result);

        // Khoảng cách vượt quá 365 ngày
        var overlongRange = await controller.Get(new DateTime(2025, 1, 1), new DateTime(2026, 2, 1));
        Assert.IsInstanceOfType<BadRequestObjectResult>(overlongRange.Result);
    }

    [TestMethod]
    public async Task PeakHoursReport_ComputesAveragesAndRatesCorrectly()
    {
        await using var db = CreateDb();
        var monday = new DateTime(2026, 10, 12); // Thứ 2
        var saturday = new DateTime(2026, 10, 17); // Thứ 7

        db.Appointments.AddRange(
            // Thứ 2 lúc 10h: 1 Completed, 1 NoShow
            new Appointment { AppointmentDate = monday, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = "Completed", BookingReference = "PK1" },
            new Appointment { AppointmentDate = monday, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = "NoShow", BookingReference = "PK2" },
            // Thứ 7 lúc 14h: 1 Completed, 1 Cancelled
            new Appointment { AppointmentDate = saturday, StartTime = TimeSpan.FromHours(14), EndTime = TimeSpan.FromHours(15), Status = "Completed", BookingReference = "PK3" },
            new Appointment { AppointmentDate = saturday, StartTime = TimeSpan.FromHours(14), EndTime = TimeSpan.FromHours(15), Status = "Cancelled", BookingReference = "PK4" });
        await db.SaveChangesAsync();

        var controller = new PeakHoursReportController(db);
        var result = await controller.Get(monday, saturday);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);
        var snapshot = ok.Value as PeakHoursReportSnapshot;
        Assert.IsNotNull(snapshot);

        Assert.AreEqual(4, snapshot.TotalAppointments);
        Assert.AreEqual(25.0m, snapshot.NoShowRate); // 1 / 4 = 25%
        Assert.AreEqual(25.0m, snapshot.CancellationRate); // 1 / 4 = 25%
        Assert.IsNotNull(snapshot.Recommendation);
    }

    [TestMethod]
    public async Task RevenueReport_AggregatesRevenueByStylistAndService()
    {
        await using var db = CreateDb();
        var date = new DateTime(2026, 10, 15);

        var stylist1 = new Stylist { FullName = "Nguyễn Hùng", Specialty = "Master", Phone = "0900000011", IsActive = true };
        var stylist2 = new Stylist { FullName = "Lê Loan", Specialty = "Stylist", Phone = "0900000012", IsActive = true };
        var service1 = new Service { ServiceName = "Nhuộm tóc", DurationMinutes = 90, Price = 500000m, IsActive = true };
        var service2 = new Service { ServiceName = "Gội đầu", DurationMinutes = 30, Price = 100000m, IsActive = true };
        db.AddRange(stylist1, stylist2, service1, service2);
        await db.SaveChangesAsync();

        // Lịch 1: Thợ 1 làm dịch vụ 1
        var app1 = new Appointment { StylistId = stylist1.StylistId, CustomerId = 1, AppointmentDate = date, Status = "Completed", BookingReference = "REV1" };
        db.Appointments.Add(app1);
        await db.SaveChangesAsync();
        db.AppointmentServices.Add(new AppointmentService { AppointmentId = app1.AppointmentId, ServiceId = service1.ServiceId });
        var inv1 = new Invoice { AppointmentId = app1.AppointmentId, Subtotal = 500000m, DiscountAmount = 0m, TotalAmount = 500000m, Status = "Paid" };
        db.Invoices.Add(inv1);
        db.Payments.Add(new Payment { InvoiceId = inv1.InvoiceId, Amount = 500000m, PaymentMethod = "Cash", PaymentStatus = "Paid", PaidAt = date });

        // Lịch 2: Thợ 2 làm dịch vụ 2
        var app2 = new Appointment { StylistId = stylist2.StylistId, CustomerId = 1, AppointmentDate = date, Status = "Completed", BookingReference = "REV2" };
        db.Appointments.Add(app2);
        await db.SaveChangesAsync();
        db.AppointmentServices.Add(new AppointmentService { AppointmentId = app2.AppointmentId, ServiceId = service2.ServiceId });
        var inv2 = new Invoice { AppointmentId = app2.AppointmentId, Subtotal = 100000m, DiscountAmount = 0m, TotalAmount = 100000m, Status = "Paid" };
        db.Invoices.Add(inv2);
        db.Payments.Add(new Payment { InvoiceId = inv2.InvoiceId, Amount = 100000m, PaymentMethod = "Transfer", PaymentStatus = "Paid", PaidAt = date });

        await db.SaveChangesAsync();

        var controller = new RevenueReportController(db);
        var result = await controller.Get(date, date);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);
        var snapshot = ok.Value as RevenueReportSnapshot;
        Assert.IsNotNull(snapshot);

        Assert.AreEqual(600000m, snapshot.TotalRevenue);
        Assert.AreEqual(2, snapshot.TotalCompleted);
        Assert.AreEqual(300000m, snapshot.AverageRevenue);

        Assert.AreEqual(2, snapshot.ByStylist.Count);
        Assert.AreEqual(2, snapshot.ByService.Count);
    }

    [TestMethod]
    public async Task RevenueReport_Export_GeneratesCsvWithUtf8Preamble()
    {
        await using var db = CreateDb();
        var date = new DateTime(2026, 10, 15);

        var controller = new RevenueReportController(db);
        var fileResult = await controller.Export(date, date) as FileContentResult;

        Assert.IsNotNull(fileResult);
        Assert.AreEqual("text/csv; charset=utf-8", fileResult.ContentType);
        Assert.IsTrue(fileResult.FileDownloadName.StartsWith("bao-cao-doanh-thu-"));

        // Kiểm tra UTF-8 BOM
        var preamble = Encoding.UTF8.GetPreamble();
        Assert.IsTrue(fileResult.FileContents.Length >= preamble.Length);
        for (int i = 0; i < preamble.Length; i++)
        {
            Assert.AreEqual(preamble[i], fileResult.FileContents[i]);
        }

        var contentString = Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.IsTrue(contentString.Contains("BÁO CÁO DOANH THU"));
        Assert.IsTrue(contentString.Contains("DOANH THU THEO THỢ"));
        Assert.IsTrue(contentString.Contains("DOANH THU THEO DỊCH VỤ"));
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
