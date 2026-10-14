using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Tests;

[TestClass]
public sealed class AppointmentBookingConcurrencyTests
{
    [TestMethod]
    public async Task Twenty_simultaneous_requests_for_one_stylist_and_slot_create_exactly_one_appointment()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(databaseName).Options;
        var date = new DateTime(2030, 1, 7);
        await using (var setup = CreateDb(options))
        {
            var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 100000, IsActive = true };
            setup.Services.Add(service);
            await setup.SaveChangesAsync();
            var stylist = new Stylist { FullName = "Thợ thử tải", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] };
            setup.Stylists.Add(stylist);
            await setup.SaveChangesAsync();
            setup.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) });
            await setup.SaveChangesAsync();
        }

        var attempts = Enumerable.Range(0, 20).Select(async number =>
        {
            await using var db = CreateDb(options);
            var booking = new AppointmentBookingService(db, TimeProvider.System);
            return await booking.CreateAsync(new BookingRequest(date, TimeSpan.FromHours(10), [1], $"Khách {number}", $"0912345{number:000}"));
        });
        var results = await Task.WhenAll(attempts);

        Assert.AreEqual(1, results.Count(result => result.Confirmation is not null));
        Assert.AreEqual(19, results.Count(result => result.ErrorCode == "slot_unavailable"));
        await using var verify = CreateDb(options);
        Assert.AreEqual(1, await verify.Appointments.CountAsync(item => item.AppointmentDate == date && item.StartTime == TimeSpan.FromHours(10)));
    }

    private static ApplicationDbContext CreateDb(DbContextOptions<ApplicationDbContext> options) => new(options, new HttpContextAccessor());
}
