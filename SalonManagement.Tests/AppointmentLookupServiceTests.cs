using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class AppointmentLookupServiceTests
{
    [TestMethod]
    public async Task Phone_lookup_orders_appointments_with_SQLite_TimeSpan_values()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();

        var customer = new Customer { FullName = "Khách tra cứu", Phone = "0912345678" };
        var stylist = new Stylist { FullName = "Thợ tra cứu", Phone = "0900000001" };
        var service = new Service { ServiceName = "Cắt tóc", DurationMinutes = 30, Price = 100000, IsActive = true };
        db.AddRange(customer, stylist, service);
        await db.SaveChangesAsync();
        db.Appointments.AddRange(
            new Appointment { CustomerId = customer.CustomerId, StylistId = stylist.StylistId, AppointmentDate = new DateTime(2030, 1, 7), StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(11.5), Status = "Confirmed", BookingReference = "LOOKUP02", AppointmentServices = [new() { ServiceId = service.ServiceId, DurationMinutes = 30, Price = service.Price }] },
            new Appointment { CustomerId = customer.CustomerId, StylistId = stylist.StylistId, AppointmentDate = new DateTime(2030, 1, 7), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5), Status = "Confirmed", BookingReference = "LOOKUP01", AppointmentServices = [new() { ServiceId = service.ServiceId, DurationMinutes = 30, Price = service.Price }] });
        await db.SaveChangesAsync();

        var result = await new AppointmentLookupService(db).SearchAsync("0912345678");

        Assert.IsNull(result.Error);
        CollectionAssert.AreEqual(new[] { "LOOKUP01", "LOOKUP02" }, result.Items.Select(item => item.Reference).ToArray());
    }

    [TestMethod]
    public async Task Reference_lookup_returns_the_matching_appointment_and_a_readable_pending_status()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        var customer = new Customer { FullName = "Khách dùng mã", Phone = "0987654321" };
        var stylist = new Stylist { FullName = "Thợ dùng mã", Phone = "0900000002" };
        var service = new Service { ServiceName = "Gội đầu", DurationMinutes = 30, Price = 80000, IsActive = true };
        db.AddRange(customer, stylist, service);
        await db.SaveChangesAsync();
        db.Appointments.Add(new Appointment
        {
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            AppointmentDate = new DateTime(2030, 1, 7),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(9.5),
            Status = "PendingConfirmation",
            BookingReference = "AB12CD34",
            AppointmentServices = [new() { ServiceId = service.ServiceId, DurationMinutes = 30, Price = service.Price }]
        });
        await db.SaveChangesAsync();

        var result = await new AppointmentLookupService(db).SearchAsync("ab12cd34");

        Assert.IsNull(result.Error);
        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual("AB12CD34", result.Items[0].Reference);
        Assert.AreEqual("Chờ xác nhận", result.Items[0].Status);
    }
}
