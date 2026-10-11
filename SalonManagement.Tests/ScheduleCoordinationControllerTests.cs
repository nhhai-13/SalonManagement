using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public sealed class ScheduleCoordinationControllerTests
{
    private static readonly DateTime SelectedDay = new(2026, 10, 11);

    [Fact]
    public async Task GetAppointments_FiltersByStylistAndSelectedCalendarDay()
    {
        await using var db = CreateDb();
        var data = await SeedBase(db);
        db.Appointments.AddRange(
            BuildAppointment(data, stylistId: data.Stylist.StylistId, SelectedDay.AddHours(10), "Pending"),
            BuildAppointment(data, stylistId: data.OtherStylist.StylistId, SelectedDay.AddHours(11), "Pending"),
            BuildAppointment(data, stylistId: data.Stylist.StylistId, SelectedDay.AddDays(1).AddHours(10), "Confirmed"));
        await db.SaveChangesAsync();

        var result = await new ScheduleCoordinationController(db).GetAppointments(
            data.Stylist.StylistId,
            DateOnly.FromDateTime(SelectedDay));

        var payload = Assert.IsType<ScheduleCoordinationAppointmentsResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        var appointment = Assert.Single(payload.Appointments);
        Assert.Equal(data.Stylist.StylistId, payload.StylistId);
        Assert.Equal(DateOnly.FromDateTime(SelectedDay), payload.Date);
        Assert.Equal(1, payload.TotalCount);
        Assert.Equal("10:00", appointment.StartTime);
    }

    [Fact]
    public async Task GetAppointments_IncludesOnlyPendingConfirmedAndArrived_EvenWhenStartTimeHasPassed()
    {
        await using var db = CreateDb();
        var data = await SeedBase(db);
        db.Appointments.AddRange(
            BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(7), "Pending"),
            BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(10), "Confirmed"),
            BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(12), "Arrived"),
            BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(13), "Completed"),
            BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(14), "Cancelled"),
            BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(15), "Canceled"),
            BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(16), "NoShow"),
            BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(17), "InProgress"));
        await db.SaveChangesAsync();

        var result = await new ScheduleCoordinationController(db).GetAppointments(
            data.Stylist.StylistId,
            DateOnly.FromDateTime(SelectedDay));

        var payload = Assert.IsType<ScheduleCoordinationAppointmentsResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(3, payload.TotalCount);
        Assert.Equal(new[] { "Pending", "Confirmed", "Arrived" },
            payload.Appointments.Select(item => item.Status));
    }

    [Fact]
    public async Task GetAppointments_ReturnsEmptyListAndZeroCountWhenNothingMatches()
    {
        await using var db = CreateDb();
        var data = await SeedBase(db);

        var result = await new ScheduleCoordinationController(db).GetAppointments(
            data.Stylist.StylistId,
            DateOnly.FromDateTime(SelectedDay));

        var payload = Assert.IsType<ScheduleCoordinationAppointmentsResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(0, payload.TotalCount);
        Assert.Empty(payload.Appointments);
    }

    [Fact]
    public async Task GetAppointments_ReturnsCustomerServiceContactAndReservedDuration()
    {
        await using var db = CreateDb();
        var data = await SeedBase(db);
        var appointment = BuildAppointment(data, data.Stylist.StylistId, SelectedDay.AddHours(9), "Confirmed");
        appointment.AppointmentServices.Add(new AppointmentService
        {
            Service = data.Service,
            ServiceId = data.Service.ServiceId,
            DurationMinutes = 30
        });
        await db.Appointments.AddAsync(appointment);
        await db.SaveChangesAsync();

        var result = await new ScheduleCoordinationController(db).GetAppointments(
            data.Stylist.StylistId,
            DateOnly.FromDateTime(SelectedDay));

        var item = Assert.Single(Assert.IsType<ScheduleCoordinationAppointmentsResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value).Appointments);
        Assert.Equal(data.Customer.FullName, item.CustomerName);
        Assert.Equal(data.Customer.Phone, item.CustomerPhone);
        Assert.Equal(data.Customer.Email, item.CustomerEmail);
        Assert.Equal(new[] { data.Service.ServiceName }, item.Services);
        Assert.Equal(60, item.DurationMinutes);
    }

    [Theory]
    [InlineData(null, "2026-10-11")]
    [InlineData(1, null)]
    [InlineData(0, "2026-10-11")]
    public async Task GetAppointments_RequiresAValidStylistAndDate(int? stylistId, string? date)
    {
        await using var db = CreateDb();
        DateOnly? selectedDate = date is null ? null : DateOnly.Parse(date);

        var result = await new ScheduleCoordinationController(db).GetAppointments(stylistId, selectedDate);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAppointments_ReturnsNotFoundForUnknownOrInactiveStylist()
    {
        await using var db = CreateDb();
        var data = await SeedBase(db);
        data.Stylist.IsActive = false;
        await db.SaveChangesAsync();

        var result = await new ScheduleCoordinationController(db).GetAppointments(
            data.Stylist.StylistId,
            DateOnly.FromDateTime(SelectedDay));

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetStylists_ReturnsOnlyActiveStylistsInNameOrder()
    {
        await using var db = CreateDb();
        var data = await SeedBase(db);
        data.OtherStylist.IsActive = false;
        await db.SaveChangesAsync();

        var result = await new ScheduleCoordinationController(db).GetStylists();

        var stylists = Assert.IsType<List<ScheduleCoordinationStylist>>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        var stylist = Assert.Single(stylists);
        Assert.Equal(data.Stylist.FullName, stylist.Name);
    }

    private static Appointment BuildAppointment(SeedData data, int stylistId, DateTime when, string status) => new()
    {
        Customer = data.Customer,
        CustomerId = data.Customer.CustomerId,
        Stylist = stylistId == data.Stylist.StylistId ? data.Stylist : data.OtherStylist,
        StylistId = stylistId,
        AppointmentDate = when.Date,
        StartTime = when.TimeOfDay,
        EndTime = when.TimeOfDay.Add(TimeSpan.FromHours(1)),
        Status = status
    };

    private static async Task<SeedData> SeedBase(ApplicationDbContext db)
    {
        var stylist = new Stylist { FullName = "Bùi Minh An", IsActive = true };
        var otherStylist = new Stylist { FullName = "Đỗ Thu Hà", IsActive = true };
        var customer = new Customer { FullName = "Nguyễn Khách", Phone = "0901234567", Email = "khach@example.com" };
        var service = new Service { ServiceName = "Cắt tóc", DurationMinutes = 30, IsActive = true };
        db.AddRange(stylist, otherStylist, customer, service);
        await db.SaveChangesAsync();
        return new SeedData(stylist, otherStylist, customer, service);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed record SeedData(Stylist Stylist, Stylist OtherStylist, Customer Customer, Service Service);
}
