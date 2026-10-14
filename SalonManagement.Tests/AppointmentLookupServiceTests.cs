using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

/// <summary>
/// Unit tests cho AppointmentLookupService (S3-05).
/// Bao gồm: tra cứu theo mã/SĐT + huỷ lịch với validate 120 phút.
/// </summary>
public class AppointmentLookupServiceTests
{
    // ============================================================
    // HELPER: Tạo DbContext InMemory
    // ============================================================
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    // ============================================================
    // HELPER: Seed Customer + Stylist + Appointment
    // ============================================================
    private static async Task<(int customerId, int stylistId)> SeedBaseAsync(ApplicationDbContext ctx)
    {
        var customer = new Customer
        {
            FullName = "Khách Test",
            Phone = "0900000001",
            IsActive = true
        };
        var stylist = new Stylist
        {
            FullName = "Thợ Test",
            Phone = "0900000002",
            IsActive = true
        };

        ctx.Customers.Add(customer);
        ctx.Stylists.Add(stylist);
        await ctx.SaveChangesAsync();

        return (customer.CustomerId, stylist.StylistId);
    }

    private static async Task<Appointment> SeedAppointmentAsync(
        ApplicationDbContext ctx,
        int customerId,
        int stylistId,
        string reference,
        DateTime date,
        TimeSpan start,
        TimeSpan end,
        string status = "Confirmed")
    {
        var appt = new Appointment
        {
            CustomerId = customerId,
            StylistId = stylistId,
            BookingReference = reference,
            AppointmentDate = date,
            StartTime = start,
            EndTime = end,
            Status = status,
            CreatedAt = DateTime.Now,
            Version = Guid.NewGuid()
        };

        ctx.Appointments.Add(appt);
        await ctx.SaveChangesAsync();
        return appt;
    }

    // TimeProvider có thể giả lập giờ cố định
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    // ============================================================
    // TEST 1: SearchAsync — mã không tồn tại
    // ============================================================
    [Fact]
    public async Task SearchAsync_UnknownReference_ReturnsError()
    {
        using var ctx = CreateContext();
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        var svc = new AppointmentLookupService(ctx, clock);

        var result = await svc.SearchAsync("ZZZZ9999");

        Assert.Empty(result.Items);
        Assert.Equal("Không tìm thấy lịch hẹn.", result.Error);
    }

    // ============================================================
    // TEST 2: SearchAsync — input rỗng
    // ============================================================
    [Fact]
    public async Task SearchAsync_EmptyInput_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = new AppointmentLookupService(ctx, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await svc.SearchAsync("");

        Assert.Empty(result.Items);
        Assert.NotNull(result.Error);
    }

    // ============================================================
    // TEST 3: SearchAsync — mã hợp lệ
    // ============================================================
    [Fact]
    public async Task SearchAsync_ValidReference_ReturnsItem()
    {
        using var ctx = CreateContext();
        var (custId, stylId) = await SeedBaseAsync(ctx);
        await SeedAppointmentAsync(ctx, custId, stylId, "ABCD2345",
            DateTime.Today.AddDays(1), new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));

        var svc = new AppointmentLookupService(ctx, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var result = await svc.SearchAsync("abcd2345"); // lowercase vẫn tìm được

        Assert.Single(result.Items);
        Assert.Equal("ABCD2345", result.Items[0].Reference);
    }

    // ============================================================
    // TEST 4: SearchAsync — SĐT (chuẩn hoá +84 → 0)
    // ============================================================
    [Fact]
    public async Task SearchAsync_PhoneWith84Prefix_NormalizesAndFinds()
    {
        using var ctx = CreateContext();
        var (custId, stylId) = await SeedBaseAsync(ctx);
        await SeedAppointmentAsync(ctx, custId, stylId, "EFGH2345",
            DateTime.Today.AddDays(1), new TimeSpan(10, 0, 0), new TimeSpan(11, 0, 0));

        var svc = new AppointmentLookupService(ctx, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var result = await svc.SearchAsync("+84 900 000 001");

        Assert.Single(result.Items);
        Assert.Equal("EFGH2345", result.Items[0].Reference);
    }

    // ============================================================
    // TEST 5: SearchAsync — SĐT loại trừ Cancelled/Completed/NoShow
    // ============================================================
    [Fact]
    public async Task SearchAsync_Phone_ExcludesCancelledAndCompletedAndNoShow()
    {
        using var ctx = CreateContext();
        var (custId, stylId) = await SeedBaseAsync(ctx);
        var date = DateTime.Today.AddDays(5);

        await SeedAppointmentAsync(ctx, custId, stylId, "AAA00001", date, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0), "Confirmed");
        await SeedAppointmentAsync(ctx, custId, stylId, "AAA00002", date, new TimeSpan(11, 0, 0), new TimeSpan(12, 0, 0), "Cancelled");
        await SeedAppointmentAsync(ctx, custId, stylId, "AAA00003", date, new TimeSpan(13, 0, 0), new TimeSpan(14, 0, 0), "Completed");
        await SeedAppointmentAsync(ctx, custId, stylId, "AAA00004", date, new TimeSpan(15, 0, 0), new TimeSpan(16, 0, 0), "NoShow");

        var svc = new AppointmentLookupService(ctx, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var result = await svc.SearchAsync("0900000001");

        Assert.Single(result.Items);
        Assert.Equal("AAA00001", result.Items[0].Reference);
    }

    // ============================================================
    // TEST 6: CancelAsync — thành công (>= 120 phút)
    // ============================================================
    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    public async Task CancelAsync_MoreThan120Minutes_SucceedsAndLookupKeepsScheduledDuration(int durationMinutes)
    {
        using var ctx = CreateContext();
        var (custId, stylId) = await SeedBaseAsync(ctx);

        var appointmentDate = new DateTime(2026, 10, 15);
        var startTime = new TimeSpan(10, 0, 0);

        var appt = await SeedAppointmentAsync(ctx, custId, stylId, "TEST0001",
            appointmentDate, startTime, startTime.Add(TimeSpan.FromMinutes(durationMinutes)));

        // Clock giả lập: 2026-10-15 07:00 (còn 3 tiếng = 180 phút)
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero)); // UTC = 07:00 VN

        var svc = new AppointmentLookupService(ctx, clock);
        var result = await svc.CancelAsync("TEST0001", "customer:0900000001", "Bận đột xuất");

        Assert.True(result.Success);
        Assert.Null(result.Error);

        // Kiểm tra DB
        var updated = await ctx.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal("Cancelled", updated!.Status);
        Assert.NotNull(updated.CancelledAt);
        Assert.Equal("customer:0900000001", updated.CancelledBy);
        Assert.Equal("Bận đột xuất", updated.CancelReason);

        // Kiểm tra audit
        var audit = ctx.AppointmentAudits.FirstOrDefault(a => a.AppointmentId == appt.AppointmentId);
        Assert.NotNull(audit);
        Assert.Equal("cancel", audit!.Action);
        Assert.Equal("Confirmed", audit.PreviousStatus);
        Assert.Equal("Cancelled", audit.NewStatus);

        // After cancellation, the lookup card must keep the duration from the actual scheduled slot.
        // The catalog snapshot is intentionally shorter to verify the appointment time is authoritative.
        var service = new Service
        {
            ServiceId = 1,
            ServiceName = "Cắt tóc nam",
            DurationMinutes = 30,
            Price = 100_000m
        };
        ctx.Services.Add(service);
        ctx.AppointmentServices.Add(new AppointmentService
        {
            Appointment = updated,
            Service = service,
            ServiceId = service.ServiceId,
            DurationMinutes = 30,
            Price = service.Price
        });
        await ctx.SaveChangesAsync();

        var lookupAfterCancellation = await svc.SearchAsync("TEST0001");
        var cancelledItem = Assert.Single(lookupAfterCancellation.Items);
        Assert.Equal("Đã huỷ", cancelledItem.Status);
        Assert.Equal(TimeSpan.FromHours(10), cancelledItem.StartTime);
        Assert.Equal(startTime.Add(TimeSpan.FromMinutes(durationMinutes)), cancelledItem.EndTime);
        Assert.Equal(durationMinutes, cancelledItem.DurationMinutes);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), cancelledItem.CancelledAt);
        Assert.Equal("Khách hàng", cancelledItem.CancelledBy);
    }

    // ============================================================
    // TEST 7: CancelAsync — thất bại (< 120 phút)
    // ============================================================
    [Fact]
    public async Task CancelAsync_LessThan120Minutes_ReturnsError()
    {
        using var ctx = CreateContext();
        var (custId, stylId) = await SeedBaseAsync(ctx);

        var appointmentDate = new DateTime(2026, 10, 15);
        var startTime = new TimeSpan(10, 0, 0);

        await SeedAppointmentAsync(ctx, custId, stylId, "TEST0002",
            appointmentDate, startTime, new TimeSpan(11, 0, 0));

        // Clock giả lập: 09:00 VN (còn 1 tiếng = 60 phút < 120)
        var clock = new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 15, 2, 0, 0, TimeSpan.Zero));

        var svc = new AppointmentLookupService(ctx, clock);
        var result = await svc.CancelAsync("TEST0002", "customer:0900000001", null);

        Assert.False(result.Success);
        Assert.Contains("120 phút", result.Error);
    }

    [Fact]
    public async Task CancelAsync_At120MinuteBoundary_ReleasesTheScheduledSlotImmediately()
    {
        using var ctx = CreateContext();
        var date = new DateTime(2030, 1, 7); // Monday
        var customer = new Customer { FullName = "Khách Test", Phone = "0900000001", IsActive = true };
        var stylist = new Stylist { FullName = "Thợ Test", IsActive = true };
        var service = new Service
        {
            ServiceName = "Cắt tóc 60 phút",
            DurationMinutes = 60,
            Price = 100_000m,
            IsActive = true
        };
        ctx.AddRange(customer, stylist, service, new BusinessHour
        {
            DayOfWeek = DayOfWeek.Monday,
            OpensAt = new TimeOnly(9, 0),
            ClosesAt = new TimeOnly(17, 0),
            IsClosed = false
        });
        await ctx.SaveChangesAsync();

        ctx.StylistServices.Add(new StylistService
        {
            StylistId = stylist.StylistId,
            ServiceId = service.ServiceId
        });
        ctx.WorkSchedules.Add(new WorkSchedule
        {
            StylistId = stylist.StylistId,
            Stylist = stylist,
            WorkDate = date,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17),
            Status = "Working"
        });
        ctx.Appointments.Add(new Appointment
        {
            Customer = customer,
            Stylist = stylist,
            AppointmentDate = date,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11),
            Status = "Confirmed",
            BookingReference = "SLOT0060",
            AppointmentServices = [new AppointmentService
            {
                Service = service,
                ServiceId = service.ServiceId,
                DurationMinutes = 60,
                Price = service.Price
            }]
        });
        await ctx.SaveChangesAsync();

        var clock = new FixedTimeProvider(new DateTimeOffset(2030, 1, 7, 1, 0, 0, TimeSpan.Zero)); // 08:00 VN
        var availability = new StylistAvailabilityService(ctx, clock);
        var day = DateOnly.FromDateTime(date);
        Assert.DoesNotContain(new StylistSlot("10:00", "11:00"),
            await availability.GetSlotsAsync([service.ServiceId], stylist.StylistId, day));

        var result = await new AppointmentLookupService(ctx, clock)
            .CancelAsync("SLOT0060", "customer:0900000001");

        Assert.True(result.Success); // Exactly 120 minutes before start is allowed.
        Assert.Contains(new StylistSlot("10:00", "11:00"),
            await availability.GetSlotsAsync([service.ServiceId], stylist.StylistId, day));
    }

    // ============================================================
    // TEST 8: CancelAsync — thất bại (đã Cancelled rồi)
    // ============================================================
    [Fact]
    public async Task CancelAsync_AlreadyCancelled_ReturnsError()
    {
        using var ctx = CreateContext();
        var (custId, stylId) = await SeedBaseAsync(ctx);

        await SeedAppointmentAsync(ctx, custId, stylId, "TEST0003",
            new DateTime(2026, 10, 15), new TimeSpan(10, 0, 0),
            new TimeSpan(11, 0, 0), "Cancelled");

        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));
        var svc = new AppointmentLookupService(ctx, clock);
        var result = await svc.CancelAsync("TEST0003", "customer:guest", null);

        Assert.False(result.Success);
        Assert.Contains("đã bị huỷ", result.Error);
    }

    // ============================================================
    // TEST 9: CancelAsync — thất bại (Completed)
    // ============================================================
    [Fact]
    public async Task CancelAsync_Completed_ReturnsError()
    {
        using var ctx = CreateContext();
        var (custId, stylId) = await SeedBaseAsync(ctx);

        await SeedAppointmentAsync(ctx, custId, stylId, "TEST0004",
            new DateTime(2026, 10, 10), new TimeSpan(10, 0, 0),
            new TimeSpan(11, 0, 0), "Completed");

        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));
        var svc = new AppointmentLookupService(ctx, clock);
        var result = await svc.CancelAsync("TEST0004", "customer:guest", null);

        Assert.False(result.Success);
        Assert.Contains("hoàn thành", result.Error);
    }

    // ============================================================
    // TEST 10: CancelAsync — thất bại (không tìm thấy)
    // ============================================================
    [Fact]
    public async Task CancelAsync_NotFound_ReturnsError()
    {
        using var ctx = CreateContext();
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        var svc = new AppointmentLookupService(ctx, clock);

        var result = await svc.CancelAsync("ZZZZ9999", "customer:guest", null);

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.Error);
    }
}
