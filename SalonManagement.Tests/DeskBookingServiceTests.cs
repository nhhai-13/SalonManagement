using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class DeskBookingServiceTests
{
    [Fact]
    public async Task Existing_customer_name_requires_confirmation_and_profile_is_preserved()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options,
            new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 14, 4, 0, 0, TimeSpan.Zero));
        var customer = new Customer { FullName = "Nguyễn Thị An", Phone = "0901234567" };
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 100000, IsActive = true };
        var stylist = new Stylist { FullName = "Thợ A", IsActive = true };
        db.AddRange(customer, service, stylist);
        await db.SaveChangesAsync();
        db.StylistServices.Add(new StylistService { StylistId = stylist.StylistId, ServiceId = service.ServiceId });
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = new DateTime(2026, 10, 14), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(18), Status = "Working" });
        await db.SaveChangesAsync();

        var request = new DeskBookingRequest
        {
            FullName = "Nguyễn Thị An Mới",
            Phone = customer.Phone,
            StylistId = stylist.StylistId,
            ServiceId = service.ServiceId,
            StartsAt = new DateTime(2026, 10, 14, 13, 0, 0)
        };
        var booking = new DeskBookingService(db, clock);
        var warning = await Assert.ThrowsAsync<InvalidOperationException>(() => booking.Create(request, "reception-user"));
        Assert.Contains("thuộc hồ sơ", warning.Message);
        Assert.Empty(db.Appointments);

        request.ConfirmExistingName = true;
        var appointment = await booking.Create(request, "reception-user");

        var savedCustomer = await db.Customers.SingleAsync();
        Assert.Equal("Nguyễn Thị An", savedCustomer.FullName);
        Assert.Equal("Confirmed", appointment.Status);
        Assert.Equal("Desk", appointment.Source);
        var audit = await db.AppointmentAudits.SingleAsync();
        Assert.Equal("reception-user", audit.ActorId);
        Assert.Equal("desk-booking", audit.Action);
    }

    [Fact]
    public async Task Desk_booking_rejects_service_not_assigned_to_stylist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options,
            new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();
        var service = new Service { ServiceName = "Nhuộm", DurationMinutes = 60, Price = 600000, IsActive = true };
        var stylist = new Stylist { FullName = "Thợ chưa có kỹ năng", IsActive = true };
        db.AddRange(service, stylist);
        await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = new DateTime(2026, 10, 14), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(18), Status = "Working" });
        await db.SaveChangesAsync();

        var request = new DeskBookingRequest
        {
            FullName = "Khách",
            Phone = "0901234567",
            StylistId = stylist.StylistId,
            ServiceId = service.ServiceId,
            StartsAt = new DateTime(2026, 10, 14, 13, 0, 0)
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DeskBookingService(db, new TestClock(new DateTimeOffset(2026, 10, 14, 4, 0, 0, TimeSpan.Zero)))
                .Create(request, "reception-user"));

        Assert.Contains("chưa được gán", error.Message);
        Assert.Empty(db.Appointments);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
