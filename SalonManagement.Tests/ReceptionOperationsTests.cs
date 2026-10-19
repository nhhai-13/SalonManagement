using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Reception;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class ReceptionOperationsTests
{
    [TestMethod]
    public async Task Snapshot_reports_live_kpis_upcoming_check_in_and_checkout_queue()
    {
        await using var db = CreateDb();
        var today = new DateTime(2031, 2, 3);
        var first = new Stylist { FullName = "An", Phone = "0900000001", IsActive = true };
        var second = new Stylist { FullName = "Bình", Phone = "0900000002", IsActive = true };
        db.AddRange(first, second);
        await db.SaveChangesAsync();
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = first.StylistId, WorkDate = today, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" },
            new WorkSchedule { StylistId = second.StylistId, WorkDate = today, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" });
        db.Appointments.AddRange(
            Appointment("UPCOMING", "Khách sắp đến", first, today, "Confirmed", 10.5, 150000),
            Appointment("ARRIVED", "Khách đang làm", first, today, "Arrived", 9, 100000),
            Appointment("CHECKOUT", "Khách cần thu", second, today, "Completed", 8, 200000),
            Appointment("CANCELLED", "Khách huỷ", second, today, "Cancelled", 13, 100000));
        await db.SaveChangesAsync();

        var clock = new FixedTimeProvider(new DateTimeOffset(2031, 2, 3, 3, 0, 0, TimeSpan.Zero)); // 10:00 tại Việt Nam
        var controller = new ReceptionOperationsController(db, new AppointmentLifecycleService(db, clock), clock);

        var action = await controller.Get();
        var snapshot = ((OkObjectResult)action.Result!).Value as ReceptionOperationsSnapshot;

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(3, snapshot.TotalAppointments);
        Assert.AreEqual(1, snapshot.CheckedInAppointments);
        Assert.AreEqual(1, snapshot.PendingCheckoutCount);
        Assert.AreEqual(1, snapshot.IdleStylistCount);
        Assert.AreEqual(2, snapshot.OnShiftStylistCount);
        Assert.AreEqual("Khách sắp đến", snapshot.UpcomingCheckIns.Single().CustomerName);
        Assert.AreEqual(200000m, snapshot.PendingCheckouts.Single().TotalAmount);
    }

    [TestMethod]
    public async Task Taking_payment_creates_paid_invoice_and_payment_record()
    {
        await using var db = CreateDb();
        var today = new DateTime(2031, 2, 3);
        var stylist = new Stylist { FullName = "An", Phone = "0900000001", IsActive = true };
        db.Add(stylist);
        await db.SaveChangesAsync();
        var completed = Appointment("DONE", "Khách cần thu", stylist, today, "Completed", 9, 250000);
        db.Add(completed);
        await db.SaveChangesAsync();
        var clock = new FixedTimeProvider(new DateTimeOffset(2031, 2, 3, 3, 0, 0, TimeSpan.Zero));
        var controller = new ReceptionOperationsController(db, new AppointmentLifecycleService(db, clock), clock);

        var action = await controller.TakePayment(completed.AppointmentId, new TakePaymentRequest("Transfer", "FT20310203"));

        Assert.IsInstanceOfType(action, typeof(OkObjectResult));
        var invoice = await db.Invoices.Include(item => item.Payments).SingleAsync();
        Assert.AreEqual("Paid", invoice.Status);
        Assert.AreEqual(250000m, invoice.TotalAmount);
        Assert.AreEqual("Transfer", invoice.Payments.Single().PaymentMethod);
        Assert.AreEqual("Paid", invoice.Payments.Single().PaymentStatus);
        Assert.AreEqual("FT20310203", invoice.Payments.Single().TransactionCode);
    }

    [TestMethod]
    public async Task Checkout_details_and_payment_use_the_services_selected_at_the_pos()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        var stylist = new Stylist { FullName = "An", Phone = "0900000001", IsActive = true };
        db.Add(stylist);
        await db.SaveChangesAsync();
        var appointment = Appointment("POS", "Khách POS", stylist, date, "Completed", 9, 150000);
        var extra = new Service { ServiceName = "Gội phát sinh", Price = 50000, DurationMinutes = 15, IsActive = true };
        db.AddRange(appointment, extra);
        await db.SaveChangesAsync();
        var clock = new FixedTimeProvider(new DateTimeOffset(2031, 2, 3, 3, 0, 0, TimeSpan.Zero));
        var controller = new ReceptionOperationsController(db, new AppointmentLifecycleService(db, clock), clock);

        var detailsAction = await controller.CheckoutDetails(appointment.AppointmentId);
        var details = ((OkObjectResult)detailsAction.Result!).Value as CheckoutDetailsResponse;
        Assert.IsNotNull(details);
        Assert.AreEqual(1, details.Services.Count);
        Assert.IsTrue(details.AvailableServices.Any(item => item.ServiceId == extra.ServiceId));

        var paymentAction = await controller.TakePayment(appointment.AppointmentId,
            new TakePaymentRequest("Cash", Services: [new CheckoutServiceChange(extra.ServiceId)]));

        Assert.IsInstanceOfType(paymentAction, typeof(OkObjectResult));
        var invoice = await db.Invoices.SingleAsync();
        Assert.AreEqual(50000m, invoice.TotalAmount);
        var selectedService = await db.AppointmentServices.SingleAsync();
        Assert.AreEqual(extra.ServiceId, selectedService.ServiceId);
    }

    private static Appointment Appointment(string reference, string name, Stylist stylist, DateTime date, string status, double hour, decimal price) => new()
    {
        BookingReference = reference,
        Customer = new Customer { FullName = name, Phone = $"091234{reference.Length:D4}" },
        Stylist = stylist,
        StylistId = stylist.StylistId,
        AppointmentDate = date,
        StartTime = TimeSpan.FromHours(hour),
        EndTime = TimeSpan.FromHours(hour + .5),
        Status = status,
        AppointmentServices = [new AppointmentService { Service = new Service { ServiceName = "Cắt", Price = price, DurationMinutes = 30 }, Price = price, DurationMinutes = 30 }]
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
