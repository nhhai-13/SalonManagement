using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class CustomerProfileManagementServiceTests
{
    [Fact]
    public async Task GetByIdAsync_CountsOnlyCompletedAndNoShowStatusesOncePerAppointment()
    {
        await using var db = CreateDb();
        db.Customers.Add(Customer());
        var visited = Appointment(1, "Completed");
        visited.AppointmentServices.Add(new AppointmentService
        {
            ServiceId = 1,
            Price = 100_000m,
            DurationMinutes = 30
        });
        visited.AppointmentServices.Add(new AppointmentService
        {
            ServiceId = 2,
            Price = 80_000m,
            DurationMinutes = 30
        });
        db.Appointments.AddRange(
            visited,
            Appointment(2, "NoShow"),
            Appointment(3, "No Show"),
            Appointment(4, "Cancelled"),
            Appointment(5, "Canceled"),
            Appointment(6, "Pending"),
            Appointment(7, "Confirmed"),
            Appointment(8, "Arrived"));
        await db.SaveChangesAsync();

        var result = await new CustomerProfileManagementService(db).GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.VisitCount);
        Assert.Equal(2, result.NoShowCount);
        Assert.Equal("0901234567", result.Phone);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsZeroCountsWhenNoAppointmentHistoryExists()
    {
        await using var db = CreateDb();
        db.Customers.Add(Customer());
        await db.SaveChangesAsync();

        var result = await new CustomerProfileManagementService(db).GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal(0, result.VisitCount);
        Assert.Equal(0, result.NoShowCount);
    }

    [Fact]
    public async Task UpdateAsync_SavesEditableFieldsAndAuditWithoutChangingPhoneIdentity()
    {
        await using var db = CreateDb();
        db.Customers.Add(Customer());
        await db.SaveChangesAsync();

        var result = await new CustomerProfileManagementService(db).UpdateAsync(
            1,
            new UpdateCustomerProfileRequest("  Tên đã sửa  ", " new@example.com ", "  Lưu ý nội bộ  "),
            "staff-123",
            "le-tan@salon.local");

        var customer = await db.Customers.SingleAsync();
        var change = await db.CustomerProfileChangeLogs.SingleAsync();
        Assert.NotNull(result);
        Assert.Equal("Tên đã sửa", customer.FullName);
        Assert.Equal("new@example.com", customer.Email);
        Assert.Equal("Lưu ý nội bộ", customer.Notes);
        Assert.Equal("0901234567", customer.Phone);
        Assert.Equal("0901234567", customer.NormalizedPhone);
        Assert.Equal("Khách ban đầu", change.PreviousFullName);
        Assert.Equal("Tên đã sửa", change.UpdatedFullName);
        Assert.Equal("staff-123", change.ChangedByUserId);
        Assert.Equal("le-tan@salon.local", change.ChangedByEmail);
        Assert.Equal(result.UpdatedAt, customer.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotCreateAuditEntryWhenNothingChanged()
    {
        await using var db = CreateDb();
        db.Customers.Add(Customer());
        await db.SaveChangesAsync();

        await new CustomerProfileManagementService(db).UpdateAsync(
            1,
            new UpdateCustomerProfileRequest("Khách ban đầu", "old@example.com", null),
            "staff-123",
            "le-tan@salon.local");

        Assert.Empty(await db.CustomerProfileChangeLogs.ToListAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenCustomerDoesNotExist()
    {
        await using var db = CreateDb();

        var result = await new CustomerProfileManagementService(db).GetByIdAsync(404);

        Assert.Null(result);
    }

    private static Customer Customer() => new()
    {
        CustomerId = 1,
        FullName = "Khách ban đầu",
        Phone = "0901234567",
        NormalizedPhone = "0901234567",
        Email = "old@example.com",
        Notes = null
    };

    private static Appointment Appointment(int id, string status) => new()
    {
        AppointmentId = id,
        CustomerId = 1,
        StylistId = 1,
        AppointmentDate = new DateTime(2026, 10, id),
        StartTime = new TimeSpan(10, 0, 0),
        EndTime = new TimeSpan(11, 0, 0),
        Status = status
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
