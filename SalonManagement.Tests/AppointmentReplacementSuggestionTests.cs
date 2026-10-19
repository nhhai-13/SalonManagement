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
public sealed class AppointmentReplacementSuggestionTests
{
    private static readonly DateOnly Date = new(2030, 1, 7);

    [TestMethod]
    public async Task Suggests_the_least_loaded_qualified_stylist_at_the_original_time()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db, firstReplacementHasSkill: true, secondReplacementHasSkill: true);

        var result = await Suggestions(db).SuggestAsync(seed.AppointmentId, seed.AbsentStylistId);

        Assert.AreEqual(seed.FirstReplacementId, result.StylistId);
        Assert.AreEqual("Thợ thay thế 1", result.StylistName);
        Assert.AreEqual(TimeSpan.FromHours(9), result.StartTime);
        Assert.AreEqual(TimeSpan.FromHours(9.5), result.EndTime);
        Assert.IsTrue(result.KeepsOriginalTime);
    }

    [TestMethod]
    public async Task Suggests_the_nearest_later_open_time_when_no_replacement_is_free_at_original_time()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db, firstReplacementHasSkill: true, secondReplacementHasSkill: false);
        db.Appointments.Add(new Appointment
        {
            Customer = new Customer { FullName = "Khách bận", Phone = "0900000009" },
            StylistId = seed.FirstReplacementId,
            AppointmentDate = Date.ToDateTime(TimeOnly.MinValue),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(9.5),
            Status = "Confirmed",
            BookingReference = "BUSY09",
            AppointmentServices = [new AppointmentService { ServiceId = seed.ServiceId, Price = 100000, DurationMinutes = 30 }]
        });
        await db.SaveChangesAsync();

        var result = await Suggestions(db).SuggestAsync(seed.AppointmentId, seed.AbsentStylistId);

        Assert.AreEqual(seed.FirstReplacementId, result.StylistId);
        Assert.AreEqual(TimeSpan.FromHours(9.5), result.StartTime);
        Assert.AreEqual(TimeSpan.FromHours(10), result.EndTime);
        Assert.IsFalse(result.KeepsOriginalTime);
    }

    [TestMethod]
    public async Task Returns_no_proposal_when_no_other_stylist_has_the_required_skill()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db, firstReplacementHasSkill: false, secondReplacementHasSkill: false);

        var result = await Suggestions(db).SuggestAsync(seed.AppointmentId, seed.AbsentStylistId);

        Assert.IsNull(result.StylistId);
        Assert.IsNull(result.StartTime);
        StringAssert.Contains(result.Message, "Không có thợ thay thế nào đủ kỹ năng");
    }

    [TestMethod]
    public async Task Returns_no_proposal_when_skilled_replacements_have_no_open_time()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db, firstReplacementHasSkill: true, secondReplacementHasSkill: false);
        db.Appointments.Add(new Appointment
        {
            Customer = new Customer { FullName = "Khách kín lịch", Phone = "0900000008" },
            StylistId = seed.FirstReplacementId,
            AppointmentDate = Date.ToDateTime(TimeOnly.MinValue),
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(17),
            Status = "Confirmed",
            BookingReference = "FULLDAY",
            AppointmentServices = [new AppointmentService { ServiceId = seed.ServiceId, Price = 100000, DurationMinutes = 30 }]
        });
        await db.SaveChangesAsync();

        var result = await Suggestions(db).SuggestAsync(seed.AppointmentId, seed.AbsentStylistId);

        Assert.IsNull(result.StylistId);
        StringAssert.Contains(result.Message, "khung giờ trống");
    }

    [TestMethod]
    public async Task Call_list_contains_only_appointments_without_a_possible_replacement_and_includes_contact_details()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db, firstReplacementHasSkill: false, secondReplacementHasSkill: false);
        var controller = new ReplacementCallListController(db, Suggestions(db));

        var action = await controller.Get(seed.AbsentStylistId, Date);
        var response = action.Result as OkObjectResult;
        var calls = response?.Value as List<ReplacementCallListItem>;

        Assert.IsNotNull(calls);
        Assert.AreEqual(1, calls.Count);
        var call = calls[0];
        Assert.AreEqual("Khách cần chuyển", call.CustomerName);
        Assert.AreEqual("0900000004", call.CustomerPhone);
        CollectionAssert.AreEqual(new[] { "Cắt kiểm thử" }, call.Services.ToArray());
        StringAssert.Contains(call.Reason, "đủ kỹ năng");
    }

    private static AppointmentReplacementSuggestionService Suggestions(ApplicationDbContext db)
        => new(db, new StylistAvailabilityService(db, new Clock()));

    private static async Task<(int AppointmentId, int AbsentStylistId, int FirstReplacementId, int ServiceId)> SeedAsync(
        ApplicationDbContext db, bool firstReplacementHasSkill, bool secondReplacementHasSkill)
    {
        var service = new Service { ServiceName = "Cắt kiểm thử", DurationMinutes = 30, Price = 100000, IsActive = true };
        var absent = new Stylist { FullName = "Thợ nghỉ", Phone = "0900000001", IsActive = true };
        var first = new Stylist { FullName = "Thợ thay thế 1", Phone = "0900000002", IsActive = true };
        var second = new Stylist { FullName = "Thợ thay thế 2", Phone = "0900000003", IsActive = true };
        db.AddRange(service, absent, first, second);
        await db.SaveChangesAsync();

        db.StylistServices.Add(new StylistService { StylistId = absent.StylistId, ServiceId = service.ServiceId });
        if (firstReplacementHasSkill) db.StylistServices.Add(new StylistService { StylistId = first.StylistId, ServiceId = service.ServiceId });
        if (secondReplacementHasSkill) db.StylistServices.Add(new StylistService { StylistId = second.StylistId, ServiceId = service.ServiceId });
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = absent.StylistId, WorkDate = Date.ToDateTime(TimeOnly.MinValue), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" },
            new WorkSchedule { StylistId = first.StylistId, WorkDate = Date.ToDateTime(TimeOnly.MinValue), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" },
            new WorkSchedule { StylistId = second.StylistId, WorkDate = Date.ToDateTime(TimeOnly.MinValue), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" });
        db.BusinessHours.Add(new BusinessHour { DayOfWeek = Date.DayOfWeek, OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(17, 0) });
        var appointment = new Appointment
        {
            Customer = new Customer { FullName = "Khách cần chuyển", Phone = "0900000004" },
            StylistId = absent.StylistId,
            AppointmentDate = Date.ToDateTime(TimeOnly.MinValue),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(9.5),
            Status = "Confirmed",
            BookingReference = "MOVE09",
            AppointmentServices = [new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes }]
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return (appointment.AppointmentId, absent.StylistId, first.StylistId, service.ServiceId);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 1, 7, 1, 0, 0, TimeSpan.Zero);
    }
}
