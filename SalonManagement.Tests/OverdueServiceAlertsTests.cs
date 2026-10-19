using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Reception;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Tests;

[TestClass]
public sealed class OverdueServiceAlertsTests
{
    [TestMethod]
    public async Task Completed_appointments_are_alerted_at_twenty_minutes_or_more_over_standard_duration()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        var service = new Service { ServiceName = "Cắt kiểm thử", DurationMinutes = 30, Price = 120000, IsActive = true };
        var stylist = new Stylist { FullName = "Thợ kiểm thử", Phone = "0987654321", IsActive = true };
        db.AddRange(service, stylist);
        await db.SaveChangesAsync();

        db.Appointments.AddRange(
            Appointment("BELOW20", "Khách dưới ngưỡng", "0900000001", stylist.StylistId, service.ServiceId, date, 49, "Completed"),
            Appointment("AT20", "Khách đúng ngưỡng", "0900000002", stylist.StylistId, service.ServiceId, date, 50, "Completed"),
            Appointment("ABOVE20", "Khách quá ngưỡng", "0900000003", stylist.StylistId, service.ServiceId, date, 55, "Completed"),
            Appointment("NOTDONE", "Khách chưa xong", "0900000004", stylist.StylistId, service.ServiceId, date, 60, "InProgress"));
        await db.SaveChangesAsync();

        var action = await new OverdueServiceAlertsController(db).Get();
        var response = action.Result as OkObjectResult;
        var alerts = response?.Value as List<OverdueServiceAlertResponse>;

        Assert.IsNotNull(alerts);
        Assert.AreEqual(2, alerts.Count);
        Assert.AreEqual("ABOVE20", alerts[0].BookingReference);
        Assert.AreEqual("Khách quá ngưỡng", alerts[0].CustomerName);
        Assert.AreEqual("Thợ kiểm thử", alerts[0].StylistName);
        Assert.AreEqual(30, alerts[0].StandardDurationMinutes);
        Assert.AreEqual(55, alerts[0].ActualDurationMinutes);
        Assert.AreEqual(25, alerts[0].ExcessDurationMinutes);
        Assert.AreEqual("AT20", alerts[1].BookingReference);
    }

    private static Appointment Appointment(string reference, string customerName, string phone, int stylistId, int serviceId, DateTime date, int actualMinutes, string status) => new()
    {
        Customer = new Customer { FullName = customerName, Phone = phone },
        StylistId = stylistId,
        AppointmentDate = date,
        StartTime = TimeSpan.FromHours(9),
        EndTime = TimeSpan.FromHours(9.5),
        Status = status,
        BookingReference = reference,
        ActualStartedAt = date.AddHours(9),
        ActualCompletedAt = date.AddHours(9).AddMinutes(actualMinutes),
        AppointmentServices = [new AppointmentService { ServiceId = serviceId, Price = 120000, DurationMinutes = 30 }]
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
