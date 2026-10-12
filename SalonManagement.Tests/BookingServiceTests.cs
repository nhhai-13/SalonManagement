using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class BookingServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesOnlineAppointmentAndNewCustomerTogether()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        SeedOptions(db);

        var result = await CreateService(db).CreateAsync(Request("0901234567"), confirmedByReception: false);

        var customer = await db.Customers.SingleAsync();
        var appointment = await db.Appointments.Include(item => item.AppointmentServices).SingleAsync();
        Assert.False(result.IsReturningCustomer);
        Assert.Equal(customer.CustomerId, appointment.CustomerId);
        Assert.Equal("Pending", appointment.Status);
        Assert.Single(appointment.AppointmentServices);
        Assert.Equal(100_000m, appointment.AppointmentServices.Single().Price);
    }

    [Fact]
    public async Task CreateAsync_ReusesProfileForReceptionAppointmentAndConfirmsIt()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        SeedOptions(db);
        var bookings = CreateService(db);

        var first = await bookings.CreateAsync(Request("0901234567"), confirmedByReception: false);
        var existingCustomer = await db.Customers.SingleAsync();
        existingCustomer.Email = "saved@example.com";
        existingCustomer.Notes = "Ghi chú được giữ lại";
        await db.SaveChangesAsync();
        var second = await bookings.CreateAsync(Request("+84 901-234-567", new TimeOnly(11, 0)), confirmedByReception: true);

        Assert.True(second.IsReturningCustomer);
        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal("Confirmed", second.Status);
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(2, await db.Appointments.CountAsync());
        Assert.Equal("saved@example.com", existingCustomer.Email);
        Assert.Equal("Ghi chú được giữ lại", existingCustomer.Notes);
    }

    [Fact]
    public async Task CreateAsync_RejectsOverlappingAppointmentForSameStylist()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        SeedOptions(db);
        var bookings = CreateService(db);
        await bookings.CreateAsync(Request("0901234567"), confirmedByReception: false);

        await Assert.ThrowsAsync<BookingConflictException>(() => bookings.CreateAsync(
            Request("0907654321", new TimeOnly(10, 15)), confirmedByReception: true));
        Assert.Equal(1, await db.Appointments.CountAsync());
        Assert.Equal(1, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsOutOfHoursAndInvalidPhonesBeforeCreatingCustomer()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        SeedOptions(db);
        var bookings = CreateService(db);

        await Assert.ThrowsAsync<BookingValidationException>(() => bookings.CreateAsync(
            Request("0901234567", new TimeOnly(16, 45)), confirmedByReception: false));
        await Assert.ThrowsAsync<ArgumentException>(() => bookings.CreateAsync(
            Request("0901234567abc"), confirmedByReception: false));

        Assert.Empty(await db.Customers.ToListAsync());
        Assert.Empty(await db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_RequiresStylistWorkingShift()
    {
        await using var db = CreateDb(Guid.NewGuid().ToString());
        SeedOptions(db);
        db.WorkSchedules.RemoveRange(db.WorkSchedules);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BookingValidationException>(() => CreateService(db)
            .CreateAsync(Request("0901234567"), confirmedByReception: false));

        Assert.Empty(await db.Customers.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_ConcurrentOnlineAndReceptionBookingsForSamePhoneReuseOneCustomer()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var seedDb = CreateDb(databaseName);
        SeedOptions(seedDb);
        await using var onlineDb = CreateDb(databaseName);
        await using var receptionDb = CreateDb(databaseName);

        var results = await Task.WhenAll(
            CreateService(onlineDb).CreateAsync(Request("0901234567", new TimeOnly(10, 0)), confirmedByReception: false),
            CreateService(receptionDb).CreateAsync(Request("+84 901-234-567", new TimeOnly(12, 0)), confirmedByReception: true));

        Assert.Single(results, result => result.IsReturningCustomer);
        await using var verifyDb = CreateDb(databaseName);
        Assert.Single(await verifyDb.Customers.ToListAsync());
        Assert.Equal(2, await verifyDb.Appointments.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WorksWithSqliteTimeAndShiftStorage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        SeedOptions(db);

        var result = await CreateService(db).CreateAsync(Request("0901234567"), confirmedByReception: false);

        Assert.False(result.IsReturningCustomer);
        Assert.Single(await db.Customers.ToListAsync());
        Assert.Single(await db.Appointments.ToListAsync());
    }

    private static BookingService CreateService(ApplicationDbContext db) =>
        new(db, new CustomerProfileService(db));

    private static CreateBookingRequest Request(string phone, TimeOnly? start = null) =>
        new("Khách thử", phone, 1, 1, DateOnly.FromDateTime(DateTime.Now).AddDays(1), start ?? new TimeOnly(10, 0));

    private static void SeedOptions(ApplicationDbContext db)
    {
        var date = DateOnly.FromDateTime(DateTime.Now).AddDays(1);
        db.Services.Add(new Service { ServiceId = 1, ServiceName = "Cắt tóc", Price = 100_000m, DurationMinutes = 60, IsActive = true });
        db.Stylists.Add(new Stylist { StylistId = 1, FullName = "Thợ A", Phone = "0900000000", IsActive = true });
        db.BusinessHours.Add(new BusinessHour
        {
            DayOfWeek = date.DayOfWeek,
            IsClosed = false,
            OpensAt = new TimeOnly(8, 0),
            ClosesAt = new TimeOnly(17, 0)
        });
        db.WorkSchedules.Add(new WorkSchedule
        {
            StylistId = 1,
            WorkDate = date.ToDateTime(TimeOnly.MinValue),
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            Status = "Working"
        });
        db.SaveChanges();
    }

    private static ApplicationDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(options);
    }
}
