using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Reception;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class CustomerProfileTests
{
    [TestMethod]
    public void Phone_normalization_matches_zero_and_plus_84_forms_and_rejects_invalid_values()
    {
        Assert.AreEqual("0912345678", BookingService.NormalizePhone("0912 345 678"));
        Assert.AreEqual("0912345678", BookingService.NormalizePhone("+84.912.345.678"));
        Assert.IsNull(BookingService.NormalizePhone("1234567890"));
        Assert.IsNull(BookingService.NormalizePhone("091234567"));
    }

    [TestMethod]
    public async Task Find_or_create_reuses_one_profile_for_equivalent_phone_formats()
    {
        await using var db = CreateDb();
        var profiles = new CustomerProfileService(db);
        var first = await profiles.FindOrCreateAsync("Khách A", "0912 345 678", "a@example.test");
        await db.SaveChangesAsync();
        var second = await profiles.FindOrCreateAsync("Khách A mới", "+84 912 345 678", null);
        await db.SaveChangesAsync();

        Assert.AreEqual(first.CustomerId, second.CustomerId);
        Assert.AreEqual(1, await db.Customers.CountAsync());
        Assert.AreEqual("a@example.test", second.Email);
    }

    [TestMethod]
    public async Task Legacy_profiles_are_consolidated_by_phone_and_second_run_is_idempotent()
    {
        await using var db = CreateDb();
        var old = new Customer { FullName = "Tên cũ", Phone = "+84 912 345 678" };
        var recent = new Customer { FullName = "Tên mới", Phone = "0912345678", Email = "new@example.test" };
        db.Customers.AddRange(old, recent); await db.SaveChangesAsync();
        db.Appointments.AddRange(
            new Appointment { CustomerId = old.CustomerId, StylistId = 1, AppointmentDate = new DateTime(2030, 1, 1), BookingReference = "OLD", Status = "Completed" },
            new Appointment { CustomerId = recent.CustomerId, StylistId = 1, AppointmentDate = new DateTime(2031, 1, 1), BookingReference = "NEW", Status = "NoShow" });
        await db.SaveChangesAsync();
        var profiles = new CustomerProfileService(db);

        var first = await profiles.ConsolidateLegacyProfilesAsync();
        var second = await profiles.ConsolidateLegacyProfilesAsync();
        var customer = await db.Customers.SingleAsync();

        Assert.AreEqual(1, first.ConsolidatedProfiles);
        Assert.AreEqual(1, first.ReassignedAppointments);
        Assert.AreEqual(0, second.ConsolidatedProfiles);
        Assert.AreEqual("0912345678", customer.Phone);
        Assert.AreEqual("Tên mới", customer.FullName);
        Assert.AreEqual(2, await db.Appointments.CountAsync(item => item.CustomerId == customer.CustomerId));
        var summary = await profiles.GetSummaryAsync(customer.CustomerId);
        Assert.AreEqual(1, summary!.VisitCount);
        Assert.AreEqual(1, summary.NoShowCount);
    }

    [TestMethod]
    public async Task Counter_appointments_attach_to_the_existing_customer_profile()
    {
        await using var db = CreateDb();
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 100000, IsActive = true };
        var stylist = new Stylist { FullName = "Thợ", Phone = "0900000001", IsActive = true };
        db.AddRange(service, stylist); await db.SaveChangesAsync();
        db.StylistServices.Add(new StylistService { StylistId = stylist.StylistId, ServiceId = service.ServiceId }); await db.SaveChangesAsync();
        var controller = new CounterAppointmentsController(db, new CustomerProfileService(db));

        var first = await controller.Create(new CounterAppointmentRequest("Khách", "0912 345 678", "customer@example.test", [service.ServiceId], stylist.StylistId, new DateTime(2031, 2, 3), TimeSpan.FromHours(9)));
        var second = await controller.Create(new CounterAppointmentRequest("Khách", "+84 912 345 678", null, [service.ServiceId], stylist.StylistId, new DateTime(2031, 2, 3), TimeSpan.FromHours(10)));

        Assert.IsInstanceOfType<OkObjectResult>(first);
        Assert.IsInstanceOfType<OkObjectResult>(second);
        Assert.AreEqual(1, await db.Customers.CountAsync());
        var ids = await db.Appointments.Select(item => item.CustomerId).Distinct().ToListAsync();
        Assert.AreEqual(1, ids.Count);
        Assert.AreEqual("Counter", (await db.Appointments.FirstAsync()).BookingSource);
    }

    [TestMethod]
    public async Task Online_booking_reuses_phone_profile_preserves_existing_email_and_fills_a_missing_email()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        var service = new Service { ServiceName = "Gội", DurationMinutes = 30, Price = 100000, IsActive = true };
        var stylist = new Stylist { FullName = "Thợ", Phone = "0900000010", IsActive = true };
        var existing = new Customer { FullName = "Tên tại quầy", Phone = "0912345678", Email = "counter@example.test" };
        var missingEmail = new Customer { FullName = "Khách thiếu email", Phone = "0912345679" };
        db.AddRange(service, stylist, existing, missingEmail); await db.SaveChangesAsync();
        db.StylistServices.Add(new StylistService { StylistId = stylist.StylistId, ServiceId = service.ServiceId });
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" });
        await db.SaveChangesAsync();
        var booking = new AppointmentBookingService(db, TimeProvider.System);

        var first = await booking.CreateAsync(new BookingRequest(date, TimeSpan.FromHours(9), [service.ServiceId], "Tên online khác", "+84 912 345 678", "online@example.test"));
        var second = await booking.CreateAsync(new BookingRequest(date, TimeSpan.FromHours(10), [service.ServiceId], "Khách thiếu email", "0912.345.679", "filled@example.test"));

        Assert.IsNotNull(first.Confirmation);
        Assert.IsNotNull(second.Confirmation);
        var matched = await db.Customers.SingleAsync(customer => customer.Phone == "0912345678");
        var filled = await db.Customers.SingleAsync(customer => customer.Phone == "0912345679");
        Assert.AreEqual("Tên tại quầy", matched.FullName);
        Assert.AreEqual("counter@example.test", matched.Email);
        Assert.AreEqual("filled@example.test", filled.Email);
        Assert.IsTrue(await db.Appointments.AllAsync(appointment => appointment.BookingSource == "Online"));
        var pendingNameReview = await db.Appointments.SingleAsync(appointment => appointment.CustomerId == matched.CustomerId);
        Assert.AreEqual("Tên online khác", pendingNameReview.CustomerNameOverride);
        Assert.AreEqual("PendingReceptionReview", pendingNameReview.NameConflictResolution);
    }

    [TestMethod]
    public async Task Online_booking_accepts_a_verified_email_contact_without_a_phone_number()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        var service = new Service { ServiceName = "Gội", DurationMinutes = 30, Price = 100000, IsActive = true };
        var stylist = new Stylist { FullName = "Thợ", Phone = "0900000010", IsActive = true };
        db.AddRange(service, stylist); await db.SaveChangesAsync();
        db.StylistServices.Add(new StylistService { StylistId = stylist.StylistId, ServiceId = service.ServiceId });
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" });
        await db.SaveChangesAsync();

        var result = await new AppointmentBookingService(db, TimeProvider.System)
            .CreateAsync(new BookingRequest(date, TimeSpan.FromHours(9), [service.ServiceId], "Khách email", null, "customer@example.test"));

        Assert.IsNotNull(result.Confirmation);
        var customer = await db.Customers.SingleAsync();
        Assert.AreEqual("customer@example.test", customer.Email);
        Assert.AreEqual(10, customer.Phone.Length);
    }

    [TestMethod]
    public async Task Summary_counts_arrived_in_progress_or_completed_visits_and_no_show()
    {
        await using var db = CreateDb();
        var customer = new Customer { FullName = "Khách", Phone = "0912345678" };
        db.Customers.Add(customer); await db.SaveChangesAsync();
        db.Appointments.AddRange(
            new Appointment { CustomerId = customer.CustomerId, StylistId = 1, AppointmentDate = DateTime.Today, BookingReference = "NEW", Status = "Confirmed" },
            new Appointment { CustomerId = customer.CustomerId, StylistId = 1, AppointmentDate = DateTime.Today, BookingReference = "ARRIVED", Status = "Arrived" },
            new Appointment { CustomerId = customer.CustomerId, StylistId = 1, AppointmentDate = DateTime.Today, BookingReference = "DONE", Status = "Completed" },
            new Appointment { CustomerId = customer.CustomerId, StylistId = 1, AppointmentDate = DateTime.Today, BookingReference = "CANCEL", Status = "Cancelled" },
            new Appointment { CustomerId = customer.CustomerId, StylistId = 1, AppointmentDate = DateTime.Today, BookingReference = "NOSHOW", Status = "NoShow" });
        await db.SaveChangesAsync();
        var profiles = new CustomerProfileService(db);

        var beforeReversal = await profiles.GetSummaryAsync(customer.CustomerId);
        Assert.AreEqual(2, beforeReversal!.VisitCount);
        Assert.AreEqual(1, beforeReversal.NoShowCount);

        (await db.Appointments.SingleAsync(item => item.BookingReference == "NOSHOW")).Status = "Confirmed";
        await db.SaveChangesAsync();
        var afterReversal = await profiles.GetSummaryAsync(customer.CustomerId);
        Assert.AreEqual(2, afterReversal!.VisitCount);
        Assert.AreEqual(0, afterReversal.NoShowCount);
    }

    [TestMethod]
    public async Task Shop_notes_persist_through_appointment_changes_and_reject_overlong_values()
    {
        await using var db = CreateDb();
        var customer = new Customer { FullName = "Khách", Phone = "0912345678" };
        db.Customers.Add(customer); await db.SaveChangesAsync();
        var appointment = new Appointment { CustomerId = customer.CustomerId, StylistId = 1, AppointmentDate = DateTime.Today, BookingReference = "NOTE", Status = "Confirmed" };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var profiles = new CustomerProfileService(db);

        await profiles.SaveShopNotesAsync(customer.CustomerId, "Thích thợ Minh, dị ứng thuốc nhuộm");
        appointment.Status = "Completed";
        await db.SaveChangesAsync();
        var saved = await profiles.GetSummaryAsync(customer.CustomerId);

        Assert.AreEqual("Thích thợ Minh, dị ứng thuốc nhuộm", saved!.ShopNotes);
        Assert.AreEqual(1, saved.VisitCount);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => profiles.SaveShopNotesAsync(customer.CustomerId, new string('x', CustomerProfileService.ShopNotesMaxLength + 1)));
    }

    [TestMethod]
    public void Receptionists_and_owners_are_authorized_to_view_and_edit_shop_notes()
    {
        var authorization = typeof(CustomerProfilesController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        Assert.AreEqual(UserRoles.Receptionist + "," + UserRoles.Owner, authorization.Roles);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
