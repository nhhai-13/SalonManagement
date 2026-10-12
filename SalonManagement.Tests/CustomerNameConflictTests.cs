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
public sealed class CustomerNameConflictTests
{
    [TestMethod]
    public async Task Equivalent_names_ignore_case_diacritics_and_extra_spaces_without_a_warning()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);

        var result = await seed.Controller.Create(Request(seed, "  lan   anh "));

        Assert.IsInstanceOfType<OkObjectResult>(result);
        Assert.AreEqual("Lan Ánh", (await db.Customers.SingleAsync()).FullName);
        Assert.IsNull((await db.Appointments.SingleAsync()).NameConflictResolution);
    }

    [TestMethod]
    public async Task Different_name_requires_a_reception_choice_and_keeps_profile_unchanged_until_then()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);

        var result = await seed.Controller.Create(Request(seed, "Lan"));

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
        Assert.AreEqual("Lan Ánh", (await db.Customers.SingleAsync()).FullName);
        Assert.AreEqual(0, await db.Appointments.CountAsync());
    }

    [TestMethod]
    public async Task Reception_can_keep_profile_name_or_use_the_entered_name_only_for_this_appointment()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);

        var keep = await seed.Controller.Create(Request(seed, "Lan", "KeepProfile", 9));
        var useForAppointment = await seed.Controller.Create(Request(seed, "Lan mới", "UseForThisAppointment", 10));

        Assert.IsInstanceOfType<OkObjectResult>(keep);
        Assert.IsInstanceOfType<OkObjectResult>(useForAppointment);
        Assert.AreEqual("Lan Ánh", (await db.Customers.SingleAsync()).FullName);
        var appointments = await db.Appointments.OrderBy(item => item.StartTime).ToListAsync();
        Assert.AreEqual("KeepProfile", appointments[0].NameConflictResolution);
        Assert.IsNull(appointments[0].CustomerNameOverride);
        Assert.AreEqual("UseForThisAppointment", appointments[1].NameConflictResolution);
        Assert.AreEqual("Lan mới", appointments[1].CustomerNameOverride);
    }

    [TestMethod]
    public async Task Reception_can_update_profile_name_and_search_immediately_uses_the_new_name()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);

        var result = await seed.Controller.Create(Request(seed, "Lan", "UpdateProfile"));

        Assert.IsInstanceOfType<OkObjectResult>(result);
        var customer = await db.Customers.SingleAsync();
        Assert.AreEqual("Lan", customer.FullName);
        var search = new CustomerProfileSearchService(db);
        Assert.AreEqual(customer.CustomerId, (await search.SearchAsync("lan")).Results.Single().CustomerId);
        Assert.AreEqual(0, (await search.SearchAsync("anh")).Results.Count);
    }

    [TestMethod]
    public async Task Online_name_difference_waits_for_reception_choice_before_confirmation()
    {
        await using var db = CreateDb();
        var customer = new Customer { FullName = "Lan Ánh", NormalizedSearchName = CustomerProfileService.NormalizeSearchName("Lan Ánh"), Phone = "0912345678" };
        var appointment = new Appointment
        {
            Customer = customer,
            StylistId = 1,
            AppointmentDate = new DateTime(2031, 2, 3),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(9.5),
            Status = "PendingConfirmation",
            BookingReference = "ONLINE01",
            BookingSource = "Online",
            CustomerNameOverride = "Lan",
            NameConflictResolution = "PendingReceptionReview"
        };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var controller = new PendingAppointmentsController(db, TimeProvider.System);

        var confirm = await controller.Confirm(appointment.AppointmentId);
        var resolve = await controller.ResolveNameConflict(appointment.AppointmentId, new ResolveNameConflictRequest("UpdateProfile"));

        Assert.IsInstanceOfType<ConflictObjectResult>(confirm);
        Assert.IsInstanceOfType<OkObjectResult>(resolve);
        var saved = await db.Appointments.Include(item => item.Customer).SingleAsync();
        Assert.AreEqual("UpdateProfile", saved.NameConflictResolution);
        Assert.IsNull(saved.CustomerNameOverride);
        Assert.AreEqual("Lan", saved.Customer.FullName);
        Assert.AreEqual(CustomerProfileService.NormalizeSearchName("Lan"), saved.Customer.NormalizedSearchName);
    }

    private static CounterAppointmentRequest Request((CounterAppointmentsController Controller, int ServiceId, int StylistId) seed, string name, string? resolution = null, int hour = 9) =>
        new(name, "0912345678", null, [seed.ServiceId], seed.StylistId, new DateTime(2031, 2, 3), TimeSpan.FromHours(hour), resolution);

    private static async Task<(CounterAppointmentsController Controller, int ServiceId, int StylistId)> SeedAsync(ApplicationDbContext db)
    {
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 100000, IsActive = true };
        var stylist = new Stylist { FullName = "Thợ", Phone = "0900000001", IsActive = true };
        var customer = new Customer { FullName = "Lan Ánh", NormalizedSearchName = CustomerProfileService.NormalizeSearchName("Lan Ánh"), Phone = "0912345678" };
        db.AddRange(service, stylist, customer); await db.SaveChangesAsync();
        db.StylistServices.Add(new StylistService { StylistId = stylist.StylistId, ServiceId = service.ServiceId }); await db.SaveChangesAsync();
        return (new CounterAppointmentsController(db, new CustomerProfileService(db)), service.ServiceId, stylist.StylistId);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
