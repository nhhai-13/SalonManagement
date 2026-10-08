using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;
namespace SalonManagement.Tests;
public class NoShowRebookingTests
{
    private class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026,10,8,9,0,0,TimeSpan.FromHours(7)); }
    [Fact]
    public async Task RebookingReleasedSlotRequiresWarningAndPreventsUndo()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var day = new DateTime(2026,10,8);
        var stylist = new Stylist { FullName = "Thợ" }; var customer = new Customer { FullName = "Khách", Phone = "0901234567" };
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 100000 };
        var old = new Appointment { Customer = customer, Stylist = stylist, AppointmentDate = day,
            StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(10), Status = "Confirmed" };
        db.AddRange(old, service); await db.SaveChangesAsync();
        db.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = day, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(18) });
        await db.SaveChangesAsync();
        var operations = new NoShowService(db, new Clock()); await operations.Apply(old.AppointmentId, "no-show", "desk");
        var booking = new DeskBookingService(db, new Clock());
        var request = new DeskBookingRequest { FullName = customer.FullName, Phone = customer.Phone,
            StylistId = stylist.StylistId, ServiceId = service.ServiceId, StartsAt = day.AddHours(9) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => booking.Create(request, "desk"));
        Assert.Single(db.Appointments);
        request.AcknowledgeNoShow = true;
        var next = await booking.Create(request, "desk");
        Assert.Equal(customer.CustomerId, next.CustomerId); Assert.Equal("Confirmed", next.Status);
        Assert.Single(await operations.OccupiedSlots(stylist.StylistId, day));
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.Apply(old.AppointmentId, "undo-no-show", "desk"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => booking.Create(request, "desk"));
        request.StartsAt = day.AddHours(19);
        await Assert.ThrowsAsync<InvalidOperationException>(() => booking.Create(request, "desk"));
    }
}
