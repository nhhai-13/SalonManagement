using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public sealed class ScheduleFollowUpTests
{
    private static readonly DateOnly Day = new(2026, 10, 11);

    [Fact]
    public async Task SyncFollowUps_AddsAppointmentsWithoutAlternativesWithReason()
    {
        await using var db = await CreateDbAsync();
        var appointment = await AddAppointmentAsync(db);
        var controller = new ScheduleCoordinationController(db);

        var sync = await controller.SyncFollowUps(new(1, Day), CancellationToken.None);
        var response = Assert.IsType<ScheduleFollowUpSyncResponse>(Assert.IsType<OkObjectResult>(sync.Result).Value);
        Assert.Equal(1, response.TotalWithoutOptions);
        Assert.Equal(ScheduleContactCategories.NoAlternative,
            (await db.ScheduleContactFollowUps.SingleAsync()).Category);

        var list = await controller.GetFollowUps(Day, null, null, CancellationToken.None);
        var item = Assert.Single(Assert.IsType<ScheduleFollowUpListResponse>(Assert.IsType<OkObjectResult>(list.Result).Value).Items);
        Assert.Equal(appointment.AppointmentId, item.AppointmentId);
        Assert.Equal("Khách cần gọi", item.CustomerName);
        Assert.Equal("0912345678", item.CustomerPhone);
        Assert.Equal(new[] { "Cắt tóc" }, item.Services);
        Assert.Equal("09:30", item.OriginalStartTime);
        Assert.Contains("kỹ năng", item.Reason);
    }

    [Fact]
    public async Task GetFollowUps_FiltersByContactStatusAndCategory()
    {
        await using var db = await CreateDbAsync();
        var appointment = await AddAppointmentAsync(db);
        var otherAppointment = await AddAppointmentAsync(db, new TimeSpan(11, 0, 0));
        db.ScheduleContactFollowUps.AddRange(
            new ScheduleContactFollowUp
            {
                AppointmentId = appointment.AppointmentId,
                Category = ScheduleContactCategories.NoAlternative,
                Reason = "Không có phương án",
                OriginalAppointmentDate = appointment.AppointmentDate,
                OriginalStartTime = appointment.StartTime,
                ContactStatus = ScheduleContactStatuses.Pending
            },
            new ScheduleContactFollowUp
            {
                AppointmentId = otherAppointment.AppointmentId,
                Category = ScheduleContactCategories.TransferFailed,
                Reason = "Xung đột",
                OriginalAppointmentDate = otherAppointment.AppointmentDate,
                OriginalStartTime = otherAppointment.StartTime,
                ContactStatus = ScheduleContactStatuses.Contacted
            });
        await db.SaveChangesAsync();

        var result = await new ScheduleCoordinationController(db).GetFollowUps(
            Day, ScheduleContactStatuses.Contacted, ScheduleContactCategories.TransferFailed, CancellationToken.None);

        var item = Assert.Single(Assert.IsType<ScheduleFollowUpListResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value).Items);
        Assert.Equal("Chuyển lịch thất bại", item.CategoryLabel);
        Assert.Equal("Xung đột", item.Reason);
    }

    [Fact]
    public async Task UpdateFollowUpContact_PersistsContactStatusAndNote()
    {
        await using var db = await CreateDbAsync();
        var appointment = await AddAppointmentAsync(db);
        var followUp = new ScheduleContactFollowUp
        {
            AppointmentId = appointment.AppointmentId,
            Category = ScheduleContactCategories.NoAlternative,
            Reason = "Không có phương án",
            OriginalAppointmentDate = appointment.AppointmentDate,
            OriginalStartTime = appointment.StartTime
        };
        db.ScheduleContactFollowUps.Add(followUp);
        await db.SaveChangesAsync();

        var result = await new ScheduleCoordinationController(db).UpdateFollowUpContact(
            followUp.ScheduleContactFollowUpId,
            new(ScheduleContactStatuses.Contacted, "Khách đồng ý chờ ngày mai"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.ScheduleContactFollowUps.AsNoTracking().SingleAsync();
        Assert.Equal(ScheduleContactStatuses.Contacted, saved.ContactStatus);
        Assert.Equal("Khách đồng ý chờ ngày mai", saved.ContactNote);
        Assert.NotNull(saved.ContactedAt);
    }

    [Fact]
    public async Task SyncFollowUps_DoesNotRelabelTransferFailureAsNoAlternative()
    {
        await using var db = await CreateDbAsync();
        var appointment = await AddAppointmentAsync(db);
        db.ScheduleContactFollowUps.Add(new ScheduleContactFollowUp
        {
            AppointmentId = appointment.AppointmentId,
            Category = ScheduleContactCategories.TransferFailed,
            Reason = "Khung giờ vừa bị chiếm",
            OriginalAppointmentDate = appointment.AppointmentDate,
            OriginalStartTime = appointment.StartTime
        });
        await db.SaveChangesAsync();

        await new ScheduleCoordinationController(db).SyncFollowUps(new(1, Day), CancellationToken.None);

        var saved = await db.ScheduleContactFollowUps.SingleAsync();
        Assert.Equal(ScheduleContactCategories.TransferFailed, saved.Category);
        Assert.Equal("Khung giờ vừa bị chiếm", saved.Reason);
    }

    private static async Task<ApplicationDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor());
        db.Stylists.AddRange(
            new Stylist { StylistId = 1, FullName = "Thợ nghỉ", IsActive = true },
            new Stylist { StylistId = 2, FullName = "Thợ khác", Specialty = "Nối mi", IsActive = true });
        db.Customers.Add(new Customer { CustomerId = 1, FullName = "Khách cần gọi", Phone = "0912345678" });
        db.Services.Add(new Service { ServiceId = 1, ServiceName = "Cắt tóc", DurationMinutes = 60, IsActive = true });
        db.BusinessHours.Add(new BusinessHour
        {
            DayOfWeek = Day.DayOfWeek,
            IsClosed = false,
            OpensAt = new TimeOnly(8, 0),
            ClosesAt = new TimeOnly(17, 0)
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<Appointment> AddAppointmentAsync(
        ApplicationDbContext db,
        TimeSpan? start = null)
    {
        var appointment = new Appointment
        {
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = Day.ToDateTime(TimeOnly.MinValue),
            StartTime = start ?? new TimeSpan(9, 30, 0),
            EndTime = (start ?? new TimeSpan(9, 30, 0)).Add(TimeSpan.FromHours(1)),
            Status = "Confirmed",
            AppointmentServices =
            [
                new AppointmentService { ServiceId = 1, DurationMinutes = 60, Price = 100_000 }
            ]
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }
}
