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

        var action = await new PendingAppointmentsController(db).Get();
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
