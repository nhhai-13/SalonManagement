using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class ScheduleTransferProcessorTests
{
    private static readonly DateOnly Day = new(2026, 10, 11);

    [Fact]
    public async Task TransferAsync_UpdatesStylistTimeAndStatusAfterCustomerConfirmation()
    {
        await using var db = await CreateDbAsync();
        var source = await AddAppointmentAsync(db, 1, new TimeSpan(9, 0, 0));
        var selection = Selection(source.AppointmentId, startMinute: 600);

        var response = await new ScheduleTransferProcessor(db).TransferAsync(new([selection]));

        var result = Assert.Single(response.Results);
        Assert.Equal("Succeeded", result.Outcome);
        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
        var saved = await db.Appointments.AsNoTracking().SingleAsync(item => item.AppointmentId == source.AppointmentId);
        Assert.Equal(2, saved.StylistId);
        Assert.Equal(new TimeSpan(10, 0, 0), saved.StartTime);
        Assert.Equal(new TimeSpan(11, 0, 0), saved.EndTime);
        Assert.Equal("Confirmed", saved.Status);
    }

    [Fact]
    public async Task TransferAsync_TransfersMultipleNonConflictingAppointmentsInOneBatch()
    {
        await using var db = await CreateDbAsync();
        var first = await AddAppointmentAsync(db, 1, new TimeSpan(9, 0, 0));
        var second = await AddAppointmentAsync(db, 1, new TimeSpan(11, 0, 0));
        var request = new ScheduleTransferBatchRequest(
        [
            Selection(first.AppointmentId, 600),
            Selection(second.AppointmentId, 720)
        ]);

        var response = await new ScheduleTransferProcessor(db).TransferAsync(request);

        Assert.Equal(2, response.SuccessCount);
        Assert.Equal(0, response.FailureCount);
        Assert.All(response.Results, result => Assert.Equal("Succeeded", result.Outcome));
        Assert.All(await db.Appointments.AsNoTracking().ToListAsync(), appointment =>
        {
            Assert.Equal(2, appointment.StylistId);
            Assert.Equal("Confirmed", appointment.Status);
        });
    }

    [Fact]
    public async Task TransferAsync_CommitsEachAppointmentIndependentlyWhenLaterChoiceConflicts()
    {
        await using var db = await CreateDbAsync();
        var first = await AddAppointmentAsync(db, 1, new TimeSpan(9, 0, 0));
        var second = await AddAppointmentAsync(db, 1, new TimeSpan(11, 0, 0));
        var request = new ScheduleTransferBatchRequest(
        [
            Selection(first.AppointmentId, 600),
            Selection(second.AppointmentId, 600)
        ]);

        var response = await new ScheduleTransferProcessor(db).TransferAsync(request);

        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        Assert.Equal("Succeeded", response.Results[0].Outcome);
        Assert.Equal("Failed", response.Results[1].Outcome);
        Assert.Contains("vừa bị chiếm", response.Results[1].Message);
        Assert.Equal(2, (await db.Appointments.AsNoTracking().SingleAsync(item => item.AppointmentId == first.AppointmentId)).StylistId);
        Assert.Equal(1, (await db.Appointments.AsNoTracking().SingleAsync(item => item.AppointmentId == second.AppointmentId)).StylistId);
    }

    [Fact]
    public async Task PreviewAsync_FindsConflictsBetweenChoicesInTheSameBatch()
    {
        await using var db = await CreateDbAsync();
        var first = await AddAppointmentAsync(db, 1, new TimeSpan(9, 0, 0));
        var second = await AddAppointmentAsync(db, 1, new TimeSpan(11, 0, 0));
        var request = new ScheduleTransferBatchRequest(
        [
            Selection(first.AppointmentId, 600),
            Selection(second.AppointmentId, 600)
        ]);

        var response = await new ScheduleTransferProcessor(db).PreviewAsync(request);

        Assert.Equal(1, response.SuccessCount);
        Assert.Equal(1, response.FailureCount);
        Assert.Equal("Ready", response.Results[0].Outcome);
        Assert.Equal("Failed", response.Results[1].Outcome);
        Assert.Contains("đã chọn trước", response.Results[1].Message);
        Assert.All(await db.Appointments.AsNoTracking().ToListAsync(), appointment =>
            Assert.Equal(1, appointment.StylistId));
    }

    [Fact]
    public async Task TransferAsync_RevalidatesAndRejectsAConflictCreatedAfterPreview()
    {
        await using var db = await CreateDbAsync();
        var source = await AddAppointmentAsync(db, 1, new TimeSpan(9, 0, 0));
        var request = new ScheduleTransferBatchRequest([Selection(source.AppointmentId, 600)]);
        var processor = new ScheduleTransferProcessor(db);

        var preview = await processor.PreviewAsync(request);
        Assert.Equal("Ready", Assert.Single(preview.Results).Outcome);
        await AddAppointmentAsync(db, 2, new TimeSpan(10, 30, 0));

        var response = await processor.TransferAsync(request);

        var result = Assert.Single(response.Results);
        Assert.Equal("Failed", result.Outcome);
        Assert.Contains("vừa bị chiếm", result.Message);
        Assert.Equal(1, (await db.Appointments.AsNoTracking().SingleAsync(item => item.AppointmentId == source.AppointmentId)).StylistId);
    }

    [Fact]
    public async Task TransferAsync_RepeatedRequestIsIdempotentAndDoesNotApplyTwice()
    {
        await using var db = await CreateDbAsync();
        var source = await AddAppointmentAsync(db, 1, new TimeSpan(9, 0, 0));
        var request = new ScheduleTransferBatchRequest([Selection(source.AppointmentId, 600)]);
        var processor = new ScheduleTransferProcessor(db);

        var first = await processor.TransferAsync(request);
        var repeated = await processor.TransferAsync(request);

        Assert.Equal("Succeeded", Assert.Single(first.Results).Outcome);
        var repeatResult = Assert.Single(repeated.Results);
        Assert.Equal("AlreadyApplied", repeatResult.Outcome);
        Assert.Equal(1, repeated.SuccessCount);
        var saved = await db.Appointments.AsNoTracking().SingleAsync(item => item.AppointmentId == source.AppointmentId);
        Assert.Equal(2, saved.StylistId);
        Assert.Equal(new TimeSpan(10, 0, 0), saved.StartTime);
    }

    [Fact]
    public async Task TransferAsync_RequiresCustomerConfirmationAndKeepsAppointmentUnchanged()
    {
        await using var db = await CreateDbAsync();
        var source = await AddAppointmentAsync(db, 1, new TimeSpan(9, 0, 0));

        var response = await new ScheduleTransferProcessor(db).TransferAsync(new(
            [Selection(source.AppointmentId, 600) with { CustomerConfirmed = false }]));

        Assert.Equal("Failed", Assert.Single(response.Results).Outcome);
        var saved = await db.Appointments.AsNoTracking().SingleAsync(item => item.AppointmentId == source.AppointmentId);
        Assert.Equal(1, saved.StylistId);
        Assert.Equal(new TimeSpan(9, 0, 0), saved.StartTime);
        Assert.Equal("Confirmed", saved.Status);
    }

    private static ScheduleTransferSelection Selection(int appointmentId, int startMinute) =>
        new(appointmentId, 2, startMinute, CustomerConfirmed: true);

    private static async Task<ApplicationDbContext> CreateDbAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);
        db.Stylists.AddRange(
            new Stylist { StylistId = 1, FullName = "Thợ nghỉ", Specialty = "Cắt tóc nam", IsActive = true },
            new Stylist
            {
                StylistId = 2,
                FullName = "Thợ thay thế",
                Specialty = "Cắt tóc nam",
                IsActive = true,
                WorkSchedules =
                [
                    new WorkSchedule
                    {
                        WorkDate = Day.ToDateTime(TimeOnly.MinValue),
                        StartTime = new TimeSpan(8, 0, 0),
                        EndTime = new TimeSpan(17, 0, 0),
                        Status = "Working"
                    }
                ]
            });
        db.Customers.Add(new Customer { CustomerId = 1, FullName = "Khách", Phone = "0900000000" });
        db.Services.Add(new Service { ServiceId = 1, ServiceName = "Cắt tóc nam", DurationMinutes = 60, IsActive = true });
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
        int stylistId,
        TimeSpan start)
    {
        var appointment = new Appointment
        {
            CustomerId = 1,
            StylistId = stylistId,
            AppointmentDate = Day.ToDateTime(TimeOnly.MinValue),
            StartTime = start,
            EndTime = start.Add(TimeSpan.FromHours(1)),
            Status = "Confirmed",
            AppointmentServices =
            [
                new AppointmentService
                {
                    ServiceId = 1,
                    DurationMinutes = 60,
                    Price = 100_000
                }
            ]
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }
}
