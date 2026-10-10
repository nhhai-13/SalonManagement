using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Claims;
using SalonManagement.Controllers.Reception;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class PendingOnlineAppointmentsTests
{
    [TestMethod]
    public async Task Online_booking_is_created_as_pending_confirmation()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        await SeedBookableStylistAsync(db, date);

        var result = await new AppointmentBookingService(db, TimeProvider.System)
            .CreateAsync(new BookingRequest(date, TimeSpan.FromHours(9), [1], "Khách online", "0912345678", "online@example.test"));

        Assert.IsNotNull(result.Confirmation);
        var appointment = await db.Appointments.SingleAsync();
        Assert.AreEqual("PendingConfirmation", appointment.Status);
    }

    [TestMethod]
    public async Task Online_booking_is_rejected_when_only_matching_stylist_is_off()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        await SeedBookableStylistAsync(db, date);
        db.StylistDaysOff.Add(new StylistDayOff { StylistId = (await db.Stylists.SingleAsync()).StylistId, OffDate = date });
        await db.SaveChangesAsync();

        var result = await new AppointmentBookingService(db, TimeProvider.System)
            .CreateAsync(new BookingRequest(date, TimeSpan.FromHours(9), [1], "Khách online", "0912345678"));

        Assert.IsNull(result.Confirmation);
        Assert.AreEqual("slot_unavailable", result.ErrorCode);
    }

    [TestMethod]
    public async Task Pending_endpoint_returns_only_online_pending_appointments_in_priority_order()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        await SeedBookableStylistAsync(db, date);
        var stylist = await db.Stylists.SingleAsync();
        var service = await db.Services.SingleAsync();
        var directCustomer = new Customer { FullName = "Khách trực tiếp", Phone = "0900000000" };
        var onlineLater = new Appointment
        {
            Customer = new Customer { FullName = "Khách sau", Phone = "0900000001", Email = "sau@example.test" },
            StylistId = stylist.StylistId,
            AppointmentDate = date,
            StartTime = TimeSpan.FromHours(11),
            EndTime = TimeSpan.FromHours(11.5),
            Status = "PendingConfirmation",
            BookingReference = "ONLINE02",
            CreatedAt = date.AddHours(8),
            AppointmentServices = [new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes }]
        };
        var onlineSooner = new Appointment
        {
            Customer = new Customer { FullName = "Khách sớm", Phone = "0900000002", Email = "som@example.test" },
            StylistId = stylist.StylistId,
            AppointmentDate = date,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(9.5),
            Status = "PendingConfirmation",
            BookingReference = "ONLINE01",
            CreatedAt = date.AddHours(9),
            AppointmentServices = [new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes }]
        };
        db.Appointments.AddRange(
            new Appointment { Customer = directCustomer, StylistId = stylist.StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(8.5), Status = "Confirmed", BookingReference = "DIRECT01" },
            onlineLater,
            onlineSooner);
        await db.SaveChangesAsync();

        var action = await new PendingAppointmentsController(db, TimeProvider.System).Get();
        var response = action.Result as OkObjectResult;
        Assert.IsNotNull(response);
        var items = response.Value as List<PendingAppointmentResponse>;
        Assert.IsNotNull(items);

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual("ONLINE01", items[0].Reference);
        Assert.AreEqual("Khách sớm", items[0].CustomerName);
        Assert.AreEqual("Thợ kiểm thử", items[0].StylistName);
        CollectionAssert.AreEqual(new[] { "Cắt kiểm thử" }, items[0].Services.ToList());
        Assert.AreEqual("PendingConfirmation", items[0].Status);
        Assert.AreEqual("ONLINE02", items[1].Reference);
    }

    [TestMethod]
    public async Task Confirm_pending_appointment_sets_status_actor_and_timestamp()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        await SeedBookableStylistAsync(db, date);
        var appointment = new Appointment { Customer = new Customer { FullName = "Khách", Phone = "0900000003" }, StylistId = (await db.Stylists.SingleAsync()).StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "PendingConfirmation", BookingReference = "ONLINE03" };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var controller = new PendingAppointmentsController(db, TimeProvider.System) { ControllerContext = new ControllerContext { HttpContext = AuthenticatedContext("reception-1") } };

        var action = await controller.Confirm(appointment.AppointmentId);

        Assert.IsInstanceOfType<OkObjectResult>(action);
        var saved = await db.Appointments.SingleAsync();
        Assert.AreEqual("Confirmed", saved.Status);
        Assert.AreEqual("reception-1", saved.ConfirmedByUserId);
        Assert.IsNotNull(saved.ConfirmedAt);
    }

    [TestMethod]
    public async Task Confirm_processed_appointment_is_rejected_without_overwriting_confirmation()
    {
        await using var db = CreateDb();
        var appointment = new Appointment { Customer = new Customer { FullName = "Khách", Phone = "0900000004" }, StylistId = 1, AppointmentDate = new DateTime(2031, 2, 3), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "Confirmed", BookingReference = "ONLINE04", ConfirmedByUserId = "first", ConfirmedAt = new DateTime(2031, 2, 1) };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var controller = new PendingAppointmentsController(db, TimeProvider.System) { ControllerContext = new ControllerContext { HttpContext = AuthenticatedContext("second") } };

        var action = await controller.Confirm(appointment.AppointmentId);

        Assert.IsInstanceOfType<ConflictObjectResult>(action);
        var saved = await db.Appointments.SingleAsync();
        Assert.AreEqual("first", saved.ConfirmedByUserId);
    }

    [TestMethod]
    public async Task Reject_pending_appointment_requires_reason_and_releases_appointment()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        await SeedBookableStylistAsync(db, date);
        var appointment = new Appointment { Customer = new Customer { FullName = "Khách", Phone = "0900000005" }, StylistId = (await db.Stylists.SingleAsync()).StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "PendingConfirmation", BookingReference = "ONLINE05" };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var controller = new PendingAppointmentsController(db, TimeProvider.System) { ControllerContext = new ControllerContext { HttpContext = AuthenticatedContext("reception-1") } };

        var missingReason = await controller.Reject(appointment.AppointmentId, new RejectPendingAppointmentRequest(null));
        var action = await controller.Reject(appointment.AppointmentId, new RejectPendingAppointmentRequest("Khung giờ không còn trống"));

        Assert.IsInstanceOfType<BadRequestObjectResult>(missingReason);
        Assert.IsInstanceOfType<OkObjectResult>(action);
        var saved = await db.Appointments.SingleAsync();
        Assert.AreEqual("Rejected", saved.Status);
        Assert.AreEqual("Khung giờ không còn trống", saved.RejectionReason);
        Assert.AreEqual("reception-1", saved.RejectedByUserId);
        Assert.IsNotNull(saved.RejectedAt);
        var slots = await new AvailabilityService(db, TimeProvider.System).GetSlotsAsync(date, [1]);
        Assert.IsTrue(slots.Slots.Contains(saved.StartTime));
    }

    [TestMethod]
    public async Task Pending_endpoint_marks_only_appointments_waiting_at_least_twelve_hours_as_overdue()
    {
        await using var db = CreateDb();
        var date = DateTime.Today.AddDays(1);
        await SeedBookableStylistAsync(db, date);
        var stylistId = (await db.Stylists.SingleAsync()).StylistId;
        db.Appointments.AddRange(
            new Appointment { Customer = new Customer { FullName = "Quá hạn", Phone = "0900000006" }, StylistId = stylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "PendingConfirmation", BookingReference = "ONLINE06", CreatedAt = DateTime.Now.AddHours(-12) },
            new Appointment { Customer = new Customer { FullName = "Còn hạn", Phone = "0900000007" }, StylistId = stylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(10.5), Status = "PendingConfirmation", BookingReference = "ONLINE07", CreatedAt = DateTime.Now.AddHours(-11).AddMinutes(-59) });
        await db.SaveChangesAsync();

        var action = await new PendingAppointmentsController(db, TimeProvider.System).Get();
        var response = action.Result as OkObjectResult;
        var items = response!.Value as List<PendingAppointmentResponse>;

        Assert.IsTrue(items!.Single(item => item.Reference == "ONLINE06").IsOverdue);
        Assert.IsFalse(items.Single(item => item.Reference == "ONLINE07").IsOverdue);
    }

    private static DefaultHttpContext AuthenticatedContext(string userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        return context;
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private static async Task SeedBookableStylistAsync(ApplicationDbContext db, DateTime date)
    {
        var service = new Service { ServiceName = "Cắt kiểm thử", DurationMinutes = 30, Price = 120000, IsActive = true };
        db.Services.Add(service);
        await db.SaveChangesAsync();
        var stylist = new Stylist
        {
            FullName = "Thợ kiểm thử",
            Phone = "0987654321",
            IsActive = true,
            Services = [new StylistService { ServiceId = service.ServiceId }]
        };
        db.Stylists.Add(stylist);
        await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(18), Status = "Working" });
        await db.SaveChangesAsync();
    }
}
