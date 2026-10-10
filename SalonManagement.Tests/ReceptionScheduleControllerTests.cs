using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Reception;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Reception;

namespace SalonManagement.Tests;

[TestClass]
public sealed class ReceptionScheduleControllerTests
{
    [TestMethod]
    public async Task Index_includes_working_stylists_without_appointments_and_keeps_exact_times()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
        var date = new DateTime(2031, 2, 3);
        var service = new Service { ServiceName = "Cắt tạo kiểu", DurationMinutes = 45, Price = 100000 };
        db.Services.Add(service); await db.SaveChangesAsync();
        var booked = new Stylist { FullName = "Thợ có lịch", Phone = "0900000001" }; var idle = new Stylist { FullName = "Thợ trống", Phone = "0900000002" };
        db.Stylists.AddRange(booked, idle); await db.SaveChangesAsync();
        db.BusinessHours.Add(new BusinessHour { DayOfWeek = date.DayOfWeek, OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(18, 0) });
        db.WorkSchedules.AddRange(new WorkSchedule { StylistId = booked.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(18) }, new WorkSchedule { StylistId = idle.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17) });
        db.Appointments.Add(new Appointment { Customer = new Customer { FullName = "Khách A", Phone = "0900000003" }, StylistId = booked.StylistId, AppointmentDate = date, StartTime = new TimeSpan(9, 10, 0), EndTime = new TimeSpan(9, 55, 0), Status = "Confirmed", BookingReference = "SCHED001", AppointmentServices = [new AppointmentService { ServiceId = service.ServiceId, DurationMinutes = 45, Price = service.Price }] });
        await db.SaveChangesAsync();

        var result = await new ReceptionScheduleController(db).Index(date);

        var model = ((ViewResult)result).Model as DailyScheduleViewModel;
        Assert.IsNotNull(model);
        var schedule = model!;
        Assert.AreEqual(2, schedule.Stylists.Count);
        Assert.AreEqual(TimeSpan.FromHours(8), schedule.OpensAt);
        var appointment = schedule.Appointments.Single();
        Assert.AreEqual("Khách A", appointment.CustomerName);
        Assert.AreEqual("Cắt tạo kiểu", appointment.Services);
        Assert.AreEqual(new TimeSpan(9, 10, 0), appointment.StartTime);
        Assert.AreEqual(new TimeSpan(9, 55, 0), appointment.EndTime);
    }
}
