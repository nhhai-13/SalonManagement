using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Reception;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Tests;

[TestClass]
public sealed class AbsentStylistAppointmentsTests
{
    [TestMethod]
    public async Task Selected_stylist_day_returns_only_unfinished_appointments_in_time_order()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        var service = new Service { ServiceName = "Gội đầu", DurationMinutes = 30, Price = 100000, IsActive = true };
        var absentStylist = new Stylist { FullName = "Thợ nghỉ", Phone = "0900000001", IsActive = true };
        var otherStylist = new Stylist { FullName = "Thợ khác", Phone = "0900000002", IsActive = true };
        db.AddRange(service, absentStylist, otherStylist);
        await db.SaveChangesAsync();

        db.Appointments.AddRange(
            Appointment("LATE", "Khách muộn", absentStylist.StylistId, service.ServiceId, date, 11, "Confirmed"),
            Appointment("EARLY", "Khách sớm", absentStylist.StylistId, service.ServiceId, date, 9, "PendingConfirmation"),
            Appointment("WORKING", "Khách đang làm", absentStylist.StylistId, service.ServiceId, date, 10, "InProgress"),
            Appointment("DONE", "Khách đã xong", absentStylist.StylistId, service.ServiceId, date, 8, "Completed"),
            Appointment("CANCEL", "Khách hủy", absentStylist.StylistId, service.ServiceId, date, 12, "Cancelled"),
            Appointment("OTHER", "Khách của thợ khác", otherStylist.StylistId, service.ServiceId, date, 8, "Confirmed"),
            Appointment("OTHERDAY", "Khách khác ngày", absentStylist.StylistId, service.ServiceId, date.AddDays(1), 8, "Confirmed"));
        await db.SaveChangesAsync();

        var action = await new AbsentStylistAppointmentsController(db).Get(absentStylist.StylistId, DateOnly.FromDateTime(date));
        var response = action.Result as OkObjectResult;
        var result = response?.Value as AbsentStylistAppointmentsResponse;

        Assert.IsNotNull(result);
        Assert.AreEqual("Thợ nghỉ", result.Stylist.FullName);
        Assert.AreEqual(DateOnly.FromDateTime(date), result.Date);
        CollectionAssert.AreEqual(new[] { "EARLY", "WORKING", "LATE" }, result.Appointments.Select(item => item.BookingReference).ToArray());
        Assert.AreEqual("Khách sớm", result.Appointments[0].CustomerName);
        CollectionAssert.AreEqual(new[] { "Gội đầu" }, result.Appointments[0].Services.ToArray());
        Assert.AreEqual(TimeSpan.FromHours(9), result.Appointments[0].StartTime);
        Assert.AreEqual(TimeSpan.FromHours(9.5), result.Appointments[0].EndTime);
        Assert.AreEqual("PendingConfirmation", result.Appointments[0].Status);
    }

    [TestMethod]
    public async Task Selected_stylist_day_with_no_unfinished_appointments_returns_an_empty_list()
    {
        await using var db = CreateDb();
        var stylist = new Stylist { FullName = "Thợ trống", Phone = "0900000003", IsActive = true };
        db.Stylists.Add(stylist);
        await db.SaveChangesAsync();

        var action = await new AbsentStylistAppointmentsController(db).Get(stylist.StylistId, new DateOnly(2031, 2, 3));
        var response = action.Result as OkObjectResult;
        var result = response?.Value as AbsentStylistAppointmentsResponse;

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Appointments.Count);
    }

    private static Appointment Appointment(string reference, string customerName, int stylistId, int serviceId, DateTime date, int hour, string status) => new()
    {
        Customer = new Customer { FullName = customerName, Phone = $"09{reference.Length}{hour:00}00000" },
        StylistId = stylistId,
        AppointmentDate = date,
        StartTime = TimeSpan.FromHours(hour),
        EndTime = TimeSpan.FromHours(hour + 0.5),
        Status = status,
        BookingReference = reference,
        AppointmentServices = [new AppointmentService { ServiceId = serviceId, Price = 100000, DurationMinutes = 30 }]
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
