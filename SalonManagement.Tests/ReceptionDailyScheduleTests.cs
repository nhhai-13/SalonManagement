using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Reception;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class ReceptionDailyScheduleTests
{
    [TestMethod]
    public async Task Daily_schedule_groups_active_stylists_and_excludes_cancelled_or_rejected_appointments()
    {
        await using var db = CreateDb();
        var date = new DateTime(2031, 2, 3);
        var first = new Stylist { FullName = "An", Phone = "0900000001", IsActive = true };
        var second = new Stylist { FullName = "Bình", Phone = "0900000002", IsActive = true };
        var inactive = new Stylist { FullName = "Cũ", Phone = "0900000003", IsActive = false };
        db.AddRange(first, second, inactive);
        db.BusinessHours.Add(new BusinessHour { DayOfWeek = date.DayOfWeek, OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(18, 0) });
        await db.SaveChangesAsync();
        var live = Appointment("LIVE", "Khách A", first.StylistId, date, "Confirmed", 9);
        db.Appointments.AddRange(
            live,
            Appointment("CANCEL", "Khách B", first.StylistId, date, "Cancelled", 10),
            Appointment("REJECT", "Khách C", second.StylistId, date, "Rejected", 11),
            Appointment("OTHER", "Khách D", second.StylistId, date.AddDays(1), "Confirmed", 9));
        db.WorkSchedules.Add(new WorkSchedule { StylistId = first.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" });
        await db.SaveChangesAsync();
        var controller = new ReceptionDailyScheduleController(db, new AppointmentLifecycleService(db, TimeProvider.System), TimeProvider.System);

        var action = await controller.Index(date);
        var view = action as ViewResult;
        var model = view?.Model as ReceptionDailyScheduleViewModel;

        Assert.IsNotNull(model);
        Assert.AreEqual(TimeSpan.FromHours(8), model.OpeningTime);
        Assert.AreEqual(TimeSpan.FromHours(18), model.ClosingTime);
        CollectionAssert.AreEqual(new[] { "An", "Bình" }, model.Stylists.Select(item => item.StylistName).ToArray());
        Assert.AreEqual("Khách A", model.Stylists[0].Appointments.Single().CustomerName);
        Assert.AreEqual(live.CustomerId, model.Stylists[0].Appointments.Single().CustomerId);
        Assert.AreEqual("Confirmed", model.Stylists[0].Appointments.Single().Status);
        Assert.AreEqual(1, model.Stylists[0].WorkingRanges.Count);
        Assert.AreEqual(0, model.Stylists[1].Appointments.Count);
    }

    [TestMethod]
    public async Task Daily_schedule_loads_and_orders_appointments_with_sqlite()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        var date = new DateTime(2031, 2, 3);
        var stylist = new Stylist { FullName = "SQLite", Phone = "0900000001", IsActive = true };
        db.Add(stylist);
        await db.SaveChangesAsync();
        db.Appointments.AddRange(
            Appointment("LATE", "Khách Muộn", stylist.StylistId, date, "Confirmed", 14),
            Appointment("EARLY", "Khách Sớm", stylist.StylistId, date, "Confirmed", 9));
        await db.SaveChangesAsync();

        var result = await new ReceptionDailyScheduleController(db, new AppointmentLifecycleService(db, TimeProvider.System), TimeProvider.System).Index(date);
        var model = ((ViewResult)result).Model as ReceptionDailyScheduleViewModel;
        Assert.IsNotNull(model);

        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromHours(9), TimeSpan.FromHours(14) },
            model!.Stylists.Single().Appointments.Select(appointment => appointment.StartTime).ToArray());
    }

    private static Appointment Appointment(string reference, string customerName, int stylistId, DateTime date, string status, int hour) => new()
    {
        Customer = new Customer { FullName = customerName, Phone = $"0912345{hour:D3}" },
        StylistId = stylistId,
        AppointmentDate = date,
        StartTime = TimeSpan.FromHours(hour),
        EndTime = TimeSpan.FromHours(hour + .5),
        BookingReference = reference,
        Status = status,
        AppointmentServices = [new AppointmentService { Service = new Service { ServiceName = "Cắt" }, DurationMinutes = 30, Price = 100000 }]
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
