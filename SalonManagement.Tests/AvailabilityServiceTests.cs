using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

/// <summary>
/// Unit tests cho AvailabilityService (S2-05).
/// Dùng EF Core InMemory để test logic thuật toán.
/// </summary>
public class AvailabilityServiceTests
{
    // ============================================================
    // HELPER: Tạo DbContext InMemory với data test
    // ============================================================
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;

        // ApplicationDbContext cần IHttpContextAccessor — tạo dummy
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        return new ApplicationDbContext(options, httpContextAccessor);
    }

    private static void SeedBusinessHours(ApplicationDbContext ctx)
    {
        for (int i = 0; i <= 6; i++)
        {
            ctx.BusinessHours.Add(new BusinessHour
            {
                DayOfWeek = (DayOfWeek)i,
                IsClosed = false,
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(21, 0),
                TimeZoneId = BusinessHour.SalonTimeZone
            });
        }
        ctx.SaveChanges();
    }

    private static void SeedServices(ApplicationDbContext ctx)
    {
        ctx.Services.AddRange(
            new Service { ServiceId = 1, ServiceName = "Cắt tóc nam", DurationMinutes = 30, Price = 100000, IsActive = true },
            new Service { ServiceId = 2, ServiceName = "Cắt tóc nữ", DurationMinutes = 45, Price = 150000, IsActive = true },
            new Service { ServiceId = 3, ServiceName = "Nhuộm dài", DurationMinutes = 120, Price = 500000, IsActive = true }
        );
        ctx.SaveChanges();
    }

    private static void SeedStylists(ApplicationDbContext ctx)
    {
        ctx.Stylists.Add(new Stylist { StylistId = 1, FullName = "A", IsActive = true });
        ctx.StylistServices.Add(new StylistService { StylistId = 1, ServiceId = 1 });

        ctx.Stylists.Add(new Stylist { StylistId = 2, FullName = "B", IsActive = true });
        ctx.StylistServices.Add(new StylistService { StylistId = 2, ServiceId = 1 });
        ctx.StylistServices.Add(new StylistService { StylistId = 2, ServiceId = 2 });

        ctx.Stylists.Add(new Stylist { StylistId = 3, FullName = "C", IsActive = true });
        ctx.StylistServices.Add(new StylistService { StylistId = 3, ServiceId = 1 });
        ctx.StylistServices.Add(new StylistService { StylistId = 3, ServiceId = 2 });
        ctx.StylistServices.Add(new StylistService { StylistId = 3, ServiceId = 3 });

        ctx.SaveChanges();
    }

    private static void SeedWorkSchedule(ApplicationDbContext ctx, int stylistId, DateTime date,
        TimeSpan start, TimeSpan end)
    {
        ctx.WorkSchedules.Add(new WorkSchedule
        {
            StylistId = stylistId,
            WorkDate = date.Date,
            StartTime = start,
            EndTime = end,
            Status = "Working"
        });
        ctx.SaveChanges();
    }

    private static void SeedAppointment(ApplicationDbContext ctx, int stylistId, DateTime date,
        TimeSpan start, TimeSpan end, string status = "Confirmed")
    {
        if (!ctx.Customers.Any(c => c.CustomerId == 1))
        {
            ctx.Customers.Add(new Customer { CustomerId = 1, FullName = "Khách Test", Phone = "0900000000", IsActive = true });
            ctx.SaveChanges();
        }

        ctx.Appointments.Add(new Appointment
        {
            CustomerId = 1,
            StylistId = stylistId,
            AppointmentDate = date.Date,
            StartTime = start,
            EndTime = end,
            Status = status
        });
        ctx.SaveChanges();
    }

    // ============================================================
    // TEST CASE 1
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_EmptyServiceIds_ReturnsEmpty()
    {
        using var ctx = CreateContext();
        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(DateTime.Today.AddDays(1), new List<int>());
        Assert.Empty(result);
    }

    // ============================================================
    // TEST CASE 2
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_PastDate_ReturnsEmpty()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        SeedWorkSchedule(ctx, 1, DateTime.Today.AddDays(-1), new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(DateTime.Today.AddDays(-1), new List<int> { 1 });
        Assert.Empty(result);
    }

    // ============================================================
    // TEST CASE 3
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_ClosedDay_ReturnsEmpty()
    {
        using var ctx = CreateContext();
        var tomorrow = DateTime.Today.AddDays(1);
        for (int i = 0; i <= 6; i++)
        {
            ctx.BusinessHours.Add(new BusinessHour
            {
                DayOfWeek = (DayOfWeek)i,
                IsClosed = (DayOfWeek)i == tomorrow.DayOfWeek,
                OpensAt = new TimeOnly(9, 0),
                ClosesAt = new TimeOnly(21, 0)
            });
        }
        ctx.SaveChanges();
        SeedServices(ctx);
        SeedStylists(ctx);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1 });
        Assert.Empty(result);
    }

    // ============================================================
    // TEST CASE 4
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_NoEligibleStylist_ReturnsEmpty()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(21, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 3 }, stylistId: 1);
        Assert.Empty(result);
    }

    // ============================================================
    // TEST CASE 5
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_SingleStylist_ReturnsSlots()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1 }, stylistId: 1);

        Assert.Equal(11, result.Count);
        Assert.Equal(new TimeSpan(9, 0, 0), result.First().StartTime);
        Assert.Equal(new TimeSpan(11, 30, 0), result.Last().StartTime);
    }

    // ============================================================
    // TEST CASE 6
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_Duration75Min_SlotSpanCorrect()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 3, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1, 2 }, stylistId: 3);

        Assert.NotEmpty(result);
        Assert.Equal(new TimeSpan(9, 0, 0), result.First().StartTime);
        Assert.Equal(new TimeSpan(10, 15, 0), result.First().EndTime);
    }

    // ============================================================
    // TEST CASE 7
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_Duration120Min_SlotSpanCorrect()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 3, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 3 }, stylistId: 3);

        Assert.NotEmpty(result);
        Assert.Equal(new TimeSpan(9, 0, 0), result.First().StartTime);
        Assert.Equal(new TimeSpan(11, 0, 0), result.First().EndTime);
    }

    // ============================================================
    // TEST CASE 8
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_ConflictWithAppointment_ExcludesSlots()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));
        SeedAppointment(ctx, 1, tomorrow, new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1 }, stylistId: 1);

        Assert.DoesNotContain(result, s => s.StartTime == new TimeSpan(9, 45, 0));
        Assert.DoesNotContain(result, s => s.StartTime == new TimeSpan(10, 0, 0));
        Assert.DoesNotContain(result, s => s.StartTime == new TimeSpan(10, 15, 0));
    }

    // ============================================================
    // TEST CASE 9
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_CancelledAppointment_DoesNotBlock()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));
        SeedAppointment(ctx, 1, tomorrow, new TimeSpan(10, 0, 0), new TimeSpan(10, 30, 0), status: "Cancelled");

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1 }, stylistId: 1);

        Assert.Contains(result, s => s.StartTime == new TimeSpan(10, 0, 0));
    }

    // ============================================================
    // TEST CASE 10
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_MultipleStylists_MergesStylistIds()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));
        SeedWorkSchedule(ctx, 2, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1 });

        var firstSlot = result.First();
        Assert.Contains(1, firstSlot.AvailableStylistIds);
        Assert.Contains(2, firstSlot.AvailableStylistIds);
    }

    // ============================================================
    // TEST CASE 11
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_PartialSkill_StylistExcluded()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));
        SeedWorkSchedule(ctx, 2, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1, 2 });

        Assert.NotEmpty(result);
        Assert.DoesNotContain(result.SelectMany(s => s.AvailableStylistIds), id => id == 1);
    }

    // ============================================================
    // TEST CASE 12
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_SlotStepIs15Minutes()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1 }, stylistId: 1);

        for (int i = 1; i < result.Count; i++)
        {
            var diff = result[i].StartTime - result[i - 1].StartTime;
            Assert.Equal(TimeSpan.FromMinutes(15), diff);
        }
    }

    // ============================================================
    // TEST CASE 13
    // ============================================================
    [Fact]
    public async Task FindNextAvailableDates_WhenNoSlotsToday_FindsNextDays()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);

        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));
        SeedAppointment(ctx, 1, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var dayAfter = DateTime.Today.AddDays(2);
        SeedWorkSchedule(ctx, 1, dayAfter, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var svc = new AvailabilityService(ctx);
        var nextDates = await svc.FindNextAvailableDatesAsync(
            tomorrow, count: 2, serviceIds: new List<int> { 1 }, stylistId: 1);

        Assert.NotEmpty(nextDates);
        Assert.Equal(dayAfter, nextDates.First());
    }

    // ============================================================
    // TEST CASE 14
    // ============================================================
    [Fact]
    public async Task FindNextAvailableDates_NoStylistSkill_ReturnsEmpty()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        SeedStylists(ctx);

        var svc = new AvailabilityService(ctx);
        var nextDates = await svc.FindNextAvailableDatesAsync(
            DateTime.Today.AddDays(1), count: 2, serviceIds: new List<int> { 99 });

        Assert.Empty(nextDates);
    }

    // ============================================================
    // TEST CASE 15
    // ============================================================
    [Fact]
    public async Task GetAvailableSlots_InactiveService_ExcludedFromDuration()
    {
        using var ctx = CreateContext();
        SeedBusinessHours(ctx);
        SeedServices(ctx);
        var s2 = ctx.Services.Find(2);
        s2!.IsActive = false;
        ctx.SaveChanges();
        SeedStylists(ctx);
        var tomorrow = DateTime.Today.AddDays(1);
        SeedWorkSchedule(ctx, 3, tomorrow, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        var svc = new AvailabilityService(ctx);
        var result = await svc.GetAvailableSlotsAsync(tomorrow, new List<int> { 1, 2 }, stylistId: 3);

        Assert.NotEmpty(result);
        Assert.Equal(new TimeSpan(9, 0, 0), result.First().StartTime);
        Assert.Equal(new TimeSpan(9, 30, 0), result.First().EndTime);
    }
}