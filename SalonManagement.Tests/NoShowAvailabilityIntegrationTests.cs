using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;
namespace SalonManagement.Tests;
public class NoShowAvailabilityIntegrationTests
{
    private class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026,10,8,9,0,0,TimeSpan.FromHours(7)); }
    [Theory] [InlineData("NoShow", true)] [InlineData("Confirmed", false)]
    public async Task TeamAvailabilityReleasesOnlyNoShowSlots(string status, bool expectedFree)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
        var day = new DateTime(2026,10,9);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 100000 };
        var stylist = new Stylist { FullName = "Thợ" };
        var customer = new Customer { FullName = "Khách", Phone = "0901234567" };
        db.AddRange(service, stylist, customer); await db.SaveChangesAsync();
        db.AddRange(new StylistService { StylistId = stylist.StylistId, ServiceId = service.ServiceId },
            new BusinessHour { DayOfWeek = day.DayOfWeek, OpensAt = new TimeOnly(8,0), ClosesAt = new TimeOnly(18,0) },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = day, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(18) },
            new Appointment { CustomerId = customer.CustomerId, StylistId = stylist.StylistId, AppointmentDate = day,
                StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(10.5), Status = status });
        await db.SaveChangesAsync();
        var slots = await new StylistAvailabilityService(db, new Clock()).GetSlotsAsync([service.ServiceId], stylist.StylistId, DateOnly.FromDateTime(day));
        Assert.Equal(expectedFree, slots.Any(s => s.Start == "10:00"));
    }
}
