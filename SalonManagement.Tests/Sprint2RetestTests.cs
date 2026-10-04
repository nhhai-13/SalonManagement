using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class Sprint2RetestTests
{
    [Fact]
    public async Task HolidayCannotHideExistingActiveAppointment()
    {
        await using var db = Db();
        db.Appointments.Add(new() { Customer = new() { FullName = "Test" }, Stylist = new() { FullName = "Stylist" },
            AppointmentDate = new(2030, 1, 7), StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = "Confirmed", BookingReference = "HOLTEST1" });
        await db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await new ShopHolidaysController(db).Create(new() { HolidayDate = new(2030, 1, 7), Reason = "Holiday" }));
        Assert.Empty(await db.ShopHolidays.ToListAsync()); Assert.Single(await db.Appointments.ToListAsync());
    }

    [Fact]
    public void HomeOpeningHoursFollowConfiguration()
    {
        var model = new SalonManagement.Models.ViewModels.HomeViewModel { BusinessHours = [
            new() { DayOfWeek = DayOfWeek.Monday, OpensAt = new(9, 0), ClosesAt = new(17, 0) },
            new() { DayOfWeek = DayOfWeek.Sunday, IsClosed = true }] };
        Assert.Equal(new[] { "Thứ 2: 09:00 – 17:00", "Chủ Nhật: Đóng cửa" }, model.OpeningHours);
    }
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());

    [Fact]
    public async Task HolidayCreationRejectsDuplicateDate()
    {
        await using var db = Db(); var controller = new ShopHolidaysController(db);
        var day = new DateOnly(2030, 1, 7);
        Assert.IsType<OkObjectResult>(await controller.Create(new() { HolidayDate = day, Reason = "Test" }));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(new() { HolidayDate = day, Reason = "Duplicate" }));
        Assert.Single(await db.ShopHolidays.ToListAsync());
    }

    [Fact]
    public async Task StylistTimeOffRejectsInvalidAndOverlappingIntervals()
    {
        await using var db = Db(); db.Stylists.Add(new() { StylistId = 1, FullName = "Test" }); await db.SaveChangesAsync();
        var controller = new StylistTimeOffsController(db); var day = new DateOnly(2030, 1, 7);
        Assert.IsType<BadRequestObjectResult>(await controller.Create(new() { StylistId = 1, OffDate = day, StartTime = new(12, 0), EndTime = new(11, 0) }));
        Assert.IsType<OkObjectResult>(await controller.Create(new() { StylistId = 1, OffDate = day, StartTime = new(10, 0), EndTime = new(11, 0) }));
        Assert.IsType<BadRequestObjectResult>(await controller.Create(new() { StylistId = 1, OffDate = day, StartTime = new(10, 30), EndTime = new(11, 30) }));
        Assert.Single(await db.StylistTimeOffs.ToListAsync());
    }

    [Fact]
    public async Task LookupReferenceShowsDetailsAndPhoneOmitsFinishedBookings()
    {
        await using var db = Db();
        var customer = new Customer { FullName = "Test", Phone = "0901234567" };
        var stylist = new Stylist { FullName = "Test stylist" };
        var service = new Service { ServiceName = "Cut", DurationMinutes = 30, Price = 100000 };
        var statuses = new[] { "Confirmed", "Pending", "InProgress", "Cancelled", "Completed", "NoShow" };
        for (var i = 0; i < statuses.Length; i++)
            db.Appointments.Add(new() { Customer = customer, Stylist = stylist, BookingReference = $"TEST000{i}",
                AppointmentDate = new(2030, 1, 7), StartTime = TimeSpan.FromHours(9 + i), EndTime = TimeSpan.FromHours(9 + i).Add(TimeSpan.FromMinutes(30)),
                Status = statuses[i], AppointmentServices = [new() { Service = service, DurationMinutes = 30, Price = 100000 }] });
        await db.SaveChangesAsync(); var lookup = new AppointmentLookupService(db);
        var item = Assert.Single((await lookup.SearchAsync("test0000")).Items);
        Assert.Equal("Test stylist", item.StylistName); Assert.Equal(30, item.DurationMinutes); Assert.Contains("Cut", item.Services);
        var active = (await lookup.SearchAsync(customer.Phone)).Items;
        Assert.Equal(3, active.Count); Assert.Equal(new[] { "TEST0000", "TEST0001", "TEST0002" }, active.Select(a => a.Reference));
        Assert.NotNull((await lookup.SearchAsync("bad")).Error);
        Assert.NotNull((await lookup.SearchAsync("NONE0000")).Error);
    }
}
