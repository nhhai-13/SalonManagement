using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Tests;

[TestClass]
public sealed class OwnerDashboardStatusFilterTests
{
    [TestMethod]
    public async Task Completed_dashboard_link_includes_completed_and_closed_appointments()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new HttpContextAccessor());
        var date = new DateTime(2031, 2, 3);
        var stylist = new Stylist { FullName = "Thợ kiểm thử", Phone = "0900000001" };
        db.Stylists.Add(stylist);
        db.Appointments.AddRange(
            CreateAppointment(date, stylist, "COMPLET1", "Completed", 9),
            CreateAppointment(date, stylist, "CLOSED01", "Closed", 10));
        await db.SaveChangesAsync();

        var result = await new AppointmentsController(db, NullLogger<AppointmentsController>.Instance).Index(date, "Completed");

        var appointments = ((ViewResult)result).Model as List<Appointment>;
        Assert.IsNotNull(appointments);
        CollectionAssert.AreEquivalent(new[] { "COMPLET1", "CLOSED01" }, appointments!.Select(item => item.BookingReference).ToArray());
    }

    private static Appointment CreateAppointment(DateTime date, Stylist stylist, string reference, string status, int hour) => new()
    {
        Customer = new Customer { FullName = "Khách", Phone = $"090000000{hour}" },
        Stylist = stylist,
        AppointmentDate = date,
        StartTime = TimeSpan.FromHours(hour),
        EndTime = TimeSpan.FromHours(hour + 1),
        Status = status,
        BookingReference = reference
    };
}
