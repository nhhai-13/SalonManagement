using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SalonManagement.Data;
using SalonManagement.Controllers;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class ScheduleChangeNotificationTests
{
    private static readonly DateOnly Day = new(2026, 10, 11);

    [Fact]
    public async Task Transfer_QueuesOneEmailForCustomerAndSnapshotsOldAndNewAppointment()
    {
        await using var db = await CreateDbAsync("khach@example.com");
        var appointment = await AddAppointmentAsync(db);
        var request = TransferRequest(appointment.AppointmentId);
        var processor = new ScheduleTransferProcessor(db);

        var response = await processor.TransferAsync(request);
        var result = Assert.Single(response.Results);
        var notification = Assert.Single(await db.ScheduleChangeNotifications.ToListAsync());

        Assert.Equal("Succeeded", result.Outcome);
        Assert.Equal(ScheduleChangeNotificationStatuses.Pending, result.EmailStatus);
        Assert.Equal(notification.ScheduleChangeNotificationId, result.EmailNotificationId);
        Assert.Equal("khach@example.com", notification.RecipientEmail);
        Assert.Equal("Khách nhận email", notification.CustomerName);
        Assert.Equal("Cắt tóc", notification.ServiceNames);
        Assert.Equal("Thợ cũ", notification.OldStylistName);
        Assert.Equal("Thợ mới", notification.NewStylistName);
        Assert.Equal(new TimeSpan(9, 0, 0), notification.OldStartTime);
        Assert.Equal(new TimeSpan(10, 0, 0), notification.NewStartTime);
        Assert.Equal(1, notification.Version);
    }

    [Fact]
    public async Task Transfer_DoesNotQueueEmailWhenCustomerHasNoEmail()
    {
        await using var db = await CreateDbAsync(email: null);
        var appointment = await AddAppointmentAsync(db);

        var response = await new ScheduleTransferProcessor(db).TransferAsync(TransferRequest(appointment.AppointmentId));

        Assert.Equal("Succeeded", Assert.Single(response.Results).Outcome);
        Assert.Equal("NotRequired", Assert.Single(response.Results).EmailStatus);
        Assert.Empty(await db.ScheduleChangeNotifications.ToListAsync());
    }

    [Fact]
    public async Task TransferFailureDoesNotCreateChangeEmail()
    {
        await using var db = await CreateDbAsync("khach@example.com");
        var appointment = await AddAppointmentAsync(db);

        var response = await new ScheduleTransferProcessor(db).TransferAsync(new(
            [TransferRequest(appointment.AppointmentId).Items[0] with { CustomerConfirmed = false }]));

        Assert.Equal("Failed", Assert.Single(response.Results).Outcome);
        Assert.Empty(await db.ScheduleChangeNotifications.ToListAsync());
        Assert.Equal(ScheduleContactCategories.TransferFailed,
            Assert.Single(await db.ScheduleContactFollowUps.ToListAsync()).Category);
    }

    [Fact]
    public async Task RepeatingSuccessfulTransferDoesNotCreateAnotherNotificationVersion()
    {
        await using var db = await CreateDbAsync("khach@example.com");
        var appointment = await AddAppointmentAsync(db);
        var processor = new ScheduleTransferProcessor(db);
        var request = TransferRequest(appointment.AppointmentId);

        await processor.TransferAsync(request);
        var retry = await processor.TransferAsync(request);

        Assert.Equal("AlreadyApplied", Assert.Single(retry.Results).Outcome);
        Assert.Single(await db.ScheduleChangeNotifications.ToListAsync());
        Assert.Equal(1, (await db.ScheduleChangeNotifications.SingleAsync()).Version);
    }

    [Fact]
    public async Task DispatcherSendsSnapshotAndRecordsSentTimeWithinFiveMinutes()
    {
        await using var db = await CreateDbAsync("khach@example.com");
        var appointment = await AddAppointmentAsync(db);
        var transfer = await new ScheduleTransferProcessor(db).TransferAsync(TransferRequest(appointment.AppointmentId));
        var transferResult = Assert.Single(transfer.Results);
        Assert.True(transferResult.Outcome == "Succeeded", transferResult.Message);
        var sender = new FakeEmailSender();
        var dispatcher = CreateDispatcher(db, sender);

        Assert.True(await dispatcher.ProcessNextAsync());

        var sent = await db.ScheduleChangeNotifications.SingleAsync();
        var email = Assert.Single(sender.Sent);
        Assert.Equal("Thợ cũ", email.OldStylistName);
        Assert.Equal("Thợ mới", email.NewStylistName);
        Assert.Equal(new TimeSpan(9, 0, 0), email.OldStartTime);
        Assert.Equal(new TimeSpan(10, 0, 0), email.NewStartTime);
        Assert.Equal(ScheduleChangeNotificationStatuses.Sent, sent.Status);
        Assert.Equal(1, sent.AttemptCount);
        Assert.NotNull(sent.SentAt);
        Assert.True(sent.SentAt - sent.TransferredAt < TimeSpan.FromMinutes(5));

        var content = SmtpScheduleChangeEmailSender.CreateHtmlBody(email, new ScheduleEmailOptions
        {
            FromName = "Salon",
            SupportPhone = "0901111222",
            SupportEmail = "support@salon.vn"
        });
        Assert.Contains("Thợ cũ", content);
        Assert.Contains("Thợ mới", content);
        Assert.Contains("09:00", content);
        Assert.Contains("10:00", content);
        Assert.Contains("support@salon.vn", content);
        Assert.Contains("0901111222", content);
    }

    [Fact]
    public async Task RelationalDispatcherClaimsNotificationAndSavesDeliveryStatus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = await CreateDbAsync("khach@example.com", options);
        var appointment = await AddAppointmentAsync(db);
        var transfer = await new ScheduleTransferProcessor(db).TransferAsync(TransferRequest(appointment.AppointmentId));
        var transferResult = Assert.Single(transfer.Results);
        Assert.True(transferResult.Outcome == "Succeeded", transferResult.Message);
        var sender = new FakeEmailSender();

        await CreateDispatcher(db, sender).ProcessNextAsync();

        var saved = await db.ScheduleChangeNotifications.AsNoTracking().SingleAsync();
        Assert.Equal(ScheduleChangeNotificationStatuses.Sent, saved.Status);
        Assert.Equal(1, saved.AttemptCount);
        Assert.NotNull(saved.SentAt);
    }

    [Fact]
    public void SqlServerMigrationScriptContainsTheOutboxTable()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=MigrationScriptCheck;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var db = new ApplicationDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor());

        var script = db.GetService<IMigrator>().GenerateScript(
            "20261011000000_AddScheduleContactFollowUps",
            "20261011010000_AddScheduleChangeNotificationOutbox");

        Assert.Contains("ScheduleChangeNotifications", script);
        Assert.Contains("IX_ScheduleChangeNotifications_AppointmentId_Version", script);
    }

    [Fact]
    public async Task NotificationStatusEndpointReturnsSentStateAndRetryCount()
    {
        await using var db = await CreateDbAsync("khach@example.com");
        var appointment = await AddAppointmentAsync(db);
        var notification = new ScheduleChangeNotification
        {
            AppointmentId = appointment.AppointmentId,
            Version = 1,
            RecipientEmail = "khach@example.com",
            CustomerName = "Khách nhận email",
            ServiceNames = "Cắt tóc",
            AppointmentDate = appointment.AppointmentDate,
            OldStylistName = "Thợ cũ",
            NewStylistName = "Thợ mới",
            OldStartTime = appointment.StartTime,
            NewStartTime = new TimeSpan(10, 0, 0),
            TransferredAt = DateTime.Now.AddMinutes(-1),
            Status = ScheduleChangeNotificationStatuses.Sent,
            AttemptCount = 2,
            SentAt = DateTime.Now
        };
        db.ScheduleChangeNotifications.Add(notification);
        await db.SaveChangesAsync();

        var result = await new ScheduleCoordinationController(db)
            .GetScheduleChangeNotification(notification.ScheduleChangeNotificationId, CancellationToken.None);

        var status = Assert.IsType<ScheduleChangeNotificationStatus>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(ScheduleChangeNotificationStatuses.Sent, status.Status);
        Assert.Equal(2, status.AttemptCount);
        Assert.NotNull(status.SentAt);
    }

    [Fact]
    public async Task DispatcherRecordsFailureAndRetriesUntilMaxAttempts()
    {
        await using var db = await CreateDbAsync("khach@example.com");
        var appointment = await AddAppointmentAsync(db);
        await new ScheduleTransferProcessor(db).TransferAsync(TransferRequest(appointment.AppointmentId));
        var sender = new FakeEmailSender { Exception = new InvalidOperationException("SMTP unavailable") };
        var dispatcher = CreateDispatcher(db, sender);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.True(await dispatcher.ProcessNextAsync());
            var notification = await db.ScheduleChangeNotifications.SingleAsync();
            Assert.Equal(attempt, notification.AttemptCount);
            if (attempt < 5)
            {
                Assert.Equal(ScheduleChangeNotificationStatuses.Pending, notification.Status);
                Assert.NotNull(notification.NextAttemptAt);
                notification.NextAttemptAt = DateTime.Now.AddSeconds(-1);
                await db.SaveChangesAsync();
            }
            else
            {
                Assert.Equal(ScheduleChangeNotificationStatuses.Failed, notification.Status);
                Assert.Null(notification.NextAttemptAt);
            }
            Assert.Contains("SMTP unavailable", notification.LastError);
        }

        Assert.Equal(5, sender.Attempts);
    }

    [Fact]
    public void EmailTemplateSubjectDescribesStylistOnlyAndStylistPlusTimeChanges()
    {
        var sameTime = new ScheduleChangeEmail("a@b.vn", "Khách", "Cắt tóc", Day.ToDateTime(TimeOnly.MinValue),
            "Thợ A", "Thợ B", new TimeSpan(9, 0, 0), new TimeSpan(9, 0, 0));
        var newTime = sameTime with { NewStartTime = new TimeSpan(10, 0, 0) };

        Assert.Contains("thợ phụ trách", SmtpScheduleChangeEmailSender.CreateSubject(sameTime), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("thợ và giờ", SmtpScheduleChangeEmailSender.CreateSubject(newTime), StringComparison.OrdinalIgnoreCase);
    }

    private static ScheduleNotificationDispatcher CreateDispatcher(ApplicationDbContext db, FakeEmailSender sender) =>
        new(db, sender,
            Options.Create(new ScheduleEmailOptions { TimeoutSeconds = 10 }),
            Options.Create(new ScheduleNotificationOptions
            {
                MaxAttempts = 5,
                RetryDelaySeconds = [1, 2, 3, 4],
                LeaseTimeoutSeconds = 30
            }),
            TimeProvider.System,
            NullLogger<ScheduleNotificationDispatcher>.Instance);

    private static ScheduleTransferBatchRequest TransferRequest(int appointmentId) =>
        new([new ScheduleTransferSelection(appointmentId, 2, 600, CustomerConfirmed: true)]);

    private static async Task<ApplicationDbContext> CreateDbAsync(
        string? email,
        DbContextOptions<ApplicationDbContext>? configuredOptions = null)
    {
        var options = configuredOptions ?? new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options, new Microsoft.AspNetCore.Http.HttpContextAccessor());
        if (db.Database.IsRelational()) await db.Database.EnsureCreatedAsync();
        db.Stylists.AddRange(
            new Stylist { StylistId = 1, FullName = "Thợ cũ", Specialty = "Cắt tóc nam", IsActive = true },
            new Stylist
            {
                StylistId = 2,
                FullName = "Thợ mới",
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
        db.Customers.Add(new Customer { CustomerId = 1, FullName = "Khách nhận email", Phone = "0900000000", Email = email });
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

    private static async Task<Appointment> AddAppointmentAsync(ApplicationDbContext db)
    {
        var appointment = new Appointment
        {
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = Day.ToDateTime(TimeOnly.MinValue),
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
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

    private sealed class FakeEmailSender : IScheduleChangeEmailSender
    {
        public List<ScheduleChangeEmail> Sent { get; } = [];
        public Exception? Exception { get; init; }
        public int Attempts { get; private set; }

        public Task SendAsync(ScheduleChangeEmail email, CancellationToken cancellationToken)
        {
            Attempts++;
            if (Exception is not null) throw Exception;
            Sent.Add(email);
            return Task.CompletedTask;
        }
    }
}
