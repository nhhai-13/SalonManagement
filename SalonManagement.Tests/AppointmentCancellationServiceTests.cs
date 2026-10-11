using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class AppointmentCancellationServiceTests
{
    [TestMethod]
    public async Task Cancel_at_or_over_120_minutes_marks_appointment_cancelled()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2031, 2, 3, 8, 0, 0, TimeSpan.FromHours(7));
        await SeedAsync(db, now.LocalDateTime.AddMinutes(120), "CANCEL01");

        var result = await new AppointmentCancellationService(db, new FakeTimeProvider(now)).CancelAsync("cancel01");

        Assert.IsTrue(result.Succeeded);
        var cancelled = await db.Appointments.SingleAsync();
        Assert.AreEqual("Cancelled", cancelled.Status);
        Assert.AreEqual("Khách hàng", cancelled.CancelledBy);
        Assert.IsNotNull(cancelled.CancelledAt);
    }

    [TestMethod]
    public async Task Cancel_under_120_minutes_is_rejected_without_changing_appointment()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2031, 2, 3, 8, 0, 0, TimeSpan.FromHours(7));
        await SeedAsync(db, now.LocalDateTime.AddMinutes(119), "CANCEL02");

        var result = await new AppointmentCancellationService(db, new FakeTimeProvider(now)).CancelAsync("CANCEL02");

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual("too_late", result.ErrorCode);
        Assert.AreEqual("Confirmed", (await db.Appointments.SingleAsync()).Status);
    }

    [TestMethod]
    public async Task Cancel_twice_is_rejected_without_overwriting_cancellation_details()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2031, 2, 3, 8, 0, 0, TimeSpan.FromHours(7));
        await SeedAsync(db, now.LocalDateTime.AddMinutes(121), "CANCEL03");
        var service = new AppointmentCancellationService(db, new FakeTimeProvider(now));
        Assert.IsTrue((await service.CancelAsync("CANCEL03")).Succeeded);
        var firstCancelledAt = (await db.Appointments.SingleAsync()).CancelledAt;

        var repeat = await service.CancelAsync("CANCEL03");

        Assert.AreEqual("already_cancelled", repeat.ErrorCode);
        Assert.AreEqual(firstCancelledAt, (await db.Appointments.SingleAsync()).CancelledAt);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
    private static async Task SeedAsync(ApplicationDbContext db, DateTime date, string reference)
    {
        db.Appointments.Add(new Appointment { Customer = new Customer { FullName = "Khách", Phone = "0900000001" }, Stylist = new Stylist { FullName = "Thợ", Phone = "0900000002" }, AppointmentDate = date.Date, StartTime = date.TimeOfDay, EndTime = date.TimeOfDay.Add(TimeSpan.FromMinutes(30)), Status = "Confirmed", BookingReference = reference });
        await db.SaveChangesAsync();
    }
    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now.ToUniversalTime(); }
}
