using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
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

    [TestMethod]
    public async Task Index_marks_day_off_and_preserves_multiple_work_intervals()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
        var date = new DateTime(2031, 2, 3);
        var working = new Stylist { FullName = "Thợ chia ca", Phone = "0900000011" };
        var off = new Stylist { FullName = "Thợ nghỉ", Phone = "0900000012" };
        db.Stylists.AddRange(working, off); await db.SaveChangesAsync();
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = working.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) },
            new WorkSchedule { StylistId = working.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(13), EndTime = TimeSpan.FromHours(17) });
        db.StylistDaysOff.Add(new StylistDayOff { StylistId = off.StylistId, OffDate = date });
        await db.SaveChangesAsync();

        var result = await new ReceptionScheduleController(db).Index(date);
        var model = (DailyScheduleViewModel)((ViewResult)result).Model!;

        var workingSchedule = model.Stylists.Single(item => item.StylistId == working.StylistId);
        Assert.IsFalse(workingSchedule.IsDayOff);
        Assert.AreEqual(2, workingSchedule.Shifts.Count);
        Assert.IsTrue(model.Stylists.Single(item => item.StylistId == off.StylistId).IsDayOff);
    }

    [TestMethod]
    public async Task Index_loads_eight_stylists_and_sixty_appointments_within_two_seconds()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
        var date = new DateTime(2031, 2, 3);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 100000 };
        db.Services.Add(service);
        for (var index = 0; index < 8; index++)
        {
            var stylist = new Stylist { FullName = $"Thợ {index + 1}", Phone = $"09000001{index:D2}" };
            db.Stylists.Add(stylist); await db.SaveChangesAsync();
            db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(18) });
            for (var appointment = 0; appointment < 8 - (index == 7 ? 4 : 0); appointment++)
                db.Appointments.Add(new Appointment { Customer = new Customer { FullName = $"Khách {index}-{appointment}", Phone = $"091{index:D1}{appointment:D6}" }, StylistId = stylist.StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(8 + appointment), EndTime = TimeSpan.FromHours(8.5 + appointment), Status = "Confirmed", BookingReference = $"LOAD{index}{appointment:D2}", AppointmentServices = [new AppointmentService { Service = service, Price = service.Price, DurationMinutes = service.DurationMinutes }] });
        }
        await db.SaveChangesAsync();
        var stopwatch = Stopwatch.StartNew();

        var result = await new ReceptionScheduleController(db).Index(date);

        stopwatch.Stop();
        var model = (DailyScheduleViewModel)((ViewResult)result).Model!;
        Assert.AreEqual(8, model.Stylists.Count);
        Assert.AreEqual(60, model.Appointments.Count);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Lịch tải mất {stopwatch.Elapsed.TotalMilliseconds:0} ms.");
    }
}
