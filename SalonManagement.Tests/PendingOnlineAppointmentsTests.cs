using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
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
        Assert.IsFalse(await db.AppointmentEmailDeliveries.AnyAsync(delivery => delivery.Kind == AppointmentEmailKinds.Confirmation),
            "Do not send a confirmation while the salon is still reviewing the booking.");
    }

    [TestMethod]
    public async Task Online_booking_keeps_existing_customer_name_and_records_source()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        await SeedBookableStylistAsync(db, date);
        db.Customers.Add(new Customer { FullName = "Tên hồ sơ", Phone = "0912345678" });
        await db.SaveChangesAsync();

        await new AppointmentBookingService(db, TimeProvider.System)
            .CreateAsync(new BookingRequest(date, TimeSpan.FromHours(9), [1], "Tên nhập mới", "0912345678"));

        Assert.AreEqual("Tên hồ sơ", (await db.Customers.SingleAsync()).FullName);
        var appointment = await db.Appointments.SingleAsync();
        Assert.AreEqual("Online", appointment.Source);
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
        var appointment = new Appointment { Customer = new Customer { FullName = "Khách", Phone = "0900000003", Email = "guest@example.test" }, StylistId = (await db.Stylists.SingleAsync()).StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "PendingConfirmation", BookingReference = "ONLINE03" };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var controller = new PendingAppointmentsController(db, TimeProvider.System) { ControllerContext = new ControllerContext { HttpContext = AuthenticatedContext("reception-1") } };

        var action = await controller.Confirm(appointment.AppointmentId);

        Assert.IsInstanceOfType<OkObjectResult>(action);
        var saved = await db.Appointments.SingleAsync();
        Assert.AreEqual("Confirmed", saved.Status);
        Assert.AreEqual("reception-1", saved.ConfirmedByUserId);
        Assert.IsNotNull(saved.ConfirmedAt);
        var confirmationEmail = await db.AppointmentEmailDeliveries.SingleAsync(delivery => delivery.Kind == AppointmentEmailKinds.Confirmation);
        Assert.AreEqual("guest@example.test", confirmationEmail.RecipientEmail);
        Assert.AreEqual(AppointmentEmailStatuses.Queued, confirmationEmail.Status);
        var reminderEmail = await db.AppointmentEmailDeliveries.SingleAsync(delivery => delivery.Kind == AppointmentEmailKinds.Reminder);
        Assert.AreEqual(AppointmentEmailStatuses.Queued, reminderEmail.Status);
    }

    [TestMethod]
    public async Task Confirm_pending_appointment_accepts_the_jwt_sub_claim_for_the_receptionist()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        await SeedBookableStylistAsync(db, date);
        var appointment = new Appointment { Customer = new Customer { FullName = "Khách", Phone = "0900000033" }, StylistId = (await db.Stylists.SingleAsync()).StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "PendingConfirmation", BookingReference = "ONLINE33" };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var controller = new PendingAppointmentsController(db, TimeProvider.System) { ControllerContext = new ControllerContext { HttpContext = AuthenticatedContext("reception-sub", "sub") } };

        var action = await controller.Confirm(appointment.AppointmentId);

        Assert.IsInstanceOfType<OkObjectResult>(action);
        Assert.AreEqual("reception-sub", (await db.Appointments.SingleAsync()).ConfirmedByUserId);
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
        var salonNow = new DateTime(2031, 2, 3, 12, 0, 0);
        var date = salonNow.Date.AddDays(1);
        await SeedBookableStylistAsync(db, date);
        var stylistId = (await db.Stylists.SingleAsync()).StylistId;
        db.Appointments.AddRange(
            new Appointment { Customer = new Customer { FullName = "Quá hạn", Phone = "0900000006" }, StylistId = stylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "PendingConfirmation", BookingReference = "ONLINE06", CreatedAt = salonNow.AddHours(-12) },
            new Appointment { Customer = new Customer { FullName = "Còn hạn", Phone = "0900000007" }, StylistId = stylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(10.5), Status = "PendingConfirmation", BookingReference = "ONLINE07", CreatedAt = salonNow.AddHours(-11).AddMinutes(-59) });
        await db.SaveChangesAsync();

        var clock = new FixedTimeProvider(new DateTimeOffset(salonNow, TimeSpan.FromHours(7)).ToUniversalTime());
        var action = await new PendingAppointmentsController(db, clock).Get();
        var response = action.Result as OkObjectResult;
 feature/integrate-sprint4-02
        var items = response!.Value as List<PendingAppointmentResponse>;
        Assert.IsNotNull(response);
        var items = response.Value as List<PendingAppointmentResponse>;
 develop
        Assert.IsNotNull(items);

        Assert.IsTrue(items.Single(item => item.Reference == "ONLINE06").IsOverdue);
        Assert.IsFalse(items.Single(item => item.Reference == "ONLINE07").IsOverdue);
    }

    [TestMethod]
    public async Task Pending_endpoint_loads_and_orders_appointments_with_sqlite()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        var date = new DateTime(2031, 2, 3);
        var stylist = new Stylist { FullName = "Thợ SQLite", Phone = "0900000008", IsActive = true };
        db.Add(stylist);
        await db.SaveChangesAsync();
        db.Appointments.AddRange(
            new Appointment { Customer = new Customer { FullName = "Khách muộn", Phone = "0900000009" }, StylistId = stylist.StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(14), EndTime = TimeSpan.FromHours(14.5), Status = "PendingConfirmation", BookingReference = "SQLITE02" },
            new Appointment { Customer = new Customer { FullName = "Khách sớm", Phone = "0900000010" }, StylistId = stylist.StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "PendingConfirmation", BookingReference = "SQLITE01" });
        await db.SaveChangesAsync();

        var action = await new PendingAppointmentsController(db, TimeProvider.System).Get();

        var response = action.Result as OkObjectResult;
        var items = response?.Value as List<PendingAppointmentResponse>;
        Assert.IsNotNull(items);
        CollectionAssert.AreEqual(new[] { "SQLITE01", "SQLITE02" }, items.Select(item => item.Reference).ToArray());
    }

    private static DefaultHttpContext AuthenticatedContext(string userId, string claimType = ClaimTypes.NameIdentifier)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, userId)], "test"));
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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
