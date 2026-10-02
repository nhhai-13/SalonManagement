using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class AvailabilityServiceTests
{
    [Fact]
    public async Task GetSlots_CombinesDurationFiltersSkillsAndDeduplicatesSlots()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 6);
        var cut = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 1 };
        var wash = new Service { ServiceName = "Gội", DurationMinutes = 45, Price = 1 };
        db.Services.AddRange(cut, wash); await db.SaveChangesAsync();
        var qualifiedA = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = cut.ServiceId }, new StylistService { ServiceId = wash.ServiceId }] };
        var qualifiedB = new Stylist { FullName = "B", Phone = "0900000002", Services = [new StylistService { ServiceId = cut.ServiceId }, new StylistService { ServiceId = wash.ServiceId }] };
        var unqualified = new Stylist { FullName = "C", Phone = "0900000003", Services = [new StylistService { ServiceId = cut.ServiceId }] };
        db.Stylists.AddRange(qualifiedA, qualifiedB, unqualified); await db.SaveChangesAsync();
        db.WorkSchedules.AddRange(new WorkSchedule { StylistId = qualifiedA.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(10) }, new WorkSchedule { StylistId = qualifiedB.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(11) }, new WorkSchedule { StylistId = unqualified.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) }); await db.SaveChangesAsync();

        var result = await new AvailabilityService(db).GetSlotsAsync(date, [cut.ServiceId, wash.ServiceId]);

        Assert.Equal(75, result.TotalDurationMinutes);
        Assert.Equal([TimeSpan.FromHours(8), TimeSpan.FromHours(8.25), TimeSpan.FromHours(8.5), TimeSpan.FromHours(8.75), TimeSpan.FromHours(9), TimeSpan.FromHours(9.25), TimeSpan.FromHours(9.5), TimeSpan.FromHours(9.75)], result.Slots);
    }

    [Fact]
    public async Task GetSlots_ExcludesFinalStartThatCannotFitDuration()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 6); var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync(); var stylist = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync(); db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9) }); await db.SaveChangesAsync();
        var result = await new AvailabilityService(db).GetSlotsAsync(date, [service.ServiceId]);
        Assert.Equal([TimeSpan.FromHours(8), TimeSpan.FromHours(8.25), TimeSpan.FromHours(8.5)], result.Slots);
    }

    [Fact]
    public async Task GetSlots_BlocksActiveAppointmentsButKeepsCancelledAppointments()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 6); var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync(); var stylist = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync(); db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(11) }); await db.SaveChangesAsync();
        db.Appointments.AddRange(new Appointment { StylistId = stylist.StylistId, CustomerId = 1, AppointmentDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10), Status = "Confirmed" }, new Appointment { StylistId = stylist.StylistId, CustomerId = 2, AppointmentDate = date, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(10.5), Status = "Cancelled" }); await db.SaveChangesAsync();
        var result = await new AvailabilityService(db).GetSlotsAsync(date, [service.ServiceId]);
        Assert.DoesNotContain(TimeSpan.FromHours(8.75), result.Slots); Assert.Contains(TimeSpan.FromHours(10), result.Slots);
    }

    [Fact]
    public async Task GetSlots_ExcludesBreaksButAllowsSlotEndingAtBreakStart()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 6);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 60, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync();
        var stylist = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(13) });
        db.StylistBreaks.Add(new StylistBreak { StylistId = stylist.StylistId, BreakDate = date, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11) }); await db.SaveChangesAsync();

        var result = await new AvailabilityService(db).GetSlotsAsync(date, [service.ServiceId]);

        Assert.Contains(TimeSpan.FromHours(9), result.Slots);
        Assert.DoesNotContain(TimeSpan.FromHours(9.25), result.Slots);
        Assert.Contains(TimeSpan.FromHours(11), result.Slots);
    }

    [Fact]
    public async Task GetSlots_RestrictsSlotsToBusinessHoursAndReturnsNoneWhenClosed()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 6);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync();
        var stylist = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) });
        db.BusinessHours.Add(new BusinessHour { DayOfWeek = date.DayOfWeek, OpensAt = new TimeOnly(9, 0), ClosesAt = new TimeOnly(12, 0) }); await db.SaveChangesAsync();

        var result = await new AvailabilityService(db).GetSlotsAsync(date, [service.ServiceId]);

        Assert.Equal(TimeSpan.FromHours(9), result.Slots.First());
        Assert.Equal(TimeSpan.FromHours(11.5), result.Slots.Last());
        db.BusinessHours.Single().IsClosed = true; await db.SaveChangesAsync();
        Assert.Empty((await new AvailabilityService(db).GetSlotsAsync(date, [service.ServiceId])).Slots);
    }

    [Fact]
    public async Task GetSlots_ExcludesStylistsWhoAreOffThatDay()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 6);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync();
        var stylist = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(10) });
        db.StylistDaysOff.Add(new StylistDayOff { StylistId = stylist.StylistId, OffDate = date }); await db.SaveChangesAsync();

        Assert.Empty((await new AvailabilityService(db).GetSlotsAsync(date, [service.ServiceId])).Slots);
    }

    [Fact]
    public async Task GetSlots_TodayShowsOnlySlotsAtLeastSixtyMinutesAhead()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 6);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync();
        var stylist = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(13) }); await db.SaveChangesAsync();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 2, 0, 0, TimeSpan.Zero)); // 09:00 in Ho Chi Minh City

        var result = await new AvailabilityService(db, clock).GetSlotsAsync(date, [service.ServiceId]);

        Assert.DoesNotContain(TimeSpan.FromHours(9.75), result.Slots);
        Assert.Contains(TimeSpan.FromHours(10), result.Slots);
    }

    [Fact]
    public async Task GetSlots_FutureDateIsNotAffectedBySixtyMinuteRule()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 7);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync();
        var stylist = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(10) }); await db.SaveChangesAsync();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 6, 2, 0, 0, TimeSpan.Zero));

        var result = await new AvailabilityService(db, clock).GetSlotsAsync(date, [service.ServiceId]);

        Assert.Contains(TimeSpan.FromHours(8), result.Slots);
    }

    [Fact]
    public async Task GetSuggestedDates_SkipsClosedAndFullyBookedDays()
    {
        await using var db = CreateDb(); var selectedDate = new DateTime(2026, 10, 10);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 60, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync();
        var stylist = new Stylist { FullName = "A", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync();
        var closedDate = selectedDate.AddDays(1); var bookedDate = selectedDate.AddDays(2); var firstAvailable = selectedDate.AddDays(3); var secondAvailable = selectedDate.AddDays(4);
        db.BusinessHours.Add(new BusinessHour { DayOfWeek = closedDate.DayOfWeek, IsClosed = true });
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = bookedDate, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9) },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = firstAvailable, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(10) },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = secondAvailable, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(10) });
        db.Appointments.Add(new Appointment { StylistId = stylist.StylistId, CustomerId = 1, AppointmentDate = bookedDate, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9), Status = "Confirmed" }); await db.SaveChangesAsync();

        var suggestions = await new AvailabilityService(db).GetSuggestedDatesAsync(selectedDate, [service.ServiceId]);

        Assert.Equal([firstAvailable, secondAvailable], suggestions.Select(item => item.Date));
        Assert.All(suggestions, item => Assert.True(item.SlotCount > 0));
        Assert.Equal([firstAvailable], (await new AvailabilityService(db).GetSuggestedDatesAsync(selectedDate, [service.ServiceId], maximumDaysToSearch: 3)).Select(item => item.Date));
        Assert.Empty(await new AvailabilityService(db).GetSuggestedDatesAsync(selectedDate, [service.ServiceId], maximumDaysToSearch: 2));
    }

    [Fact]
    public async Task CreateBooking_CreatesSummaryAndRejectsASecondBookingForTheSameSlot()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 10);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 60, Price = 100_000 }; db.Services.Add(service); await db.SaveChangesAsync();
        var stylist = new Stylist { FullName = "Mai", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; db.Stylists.Add(stylist); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) }); await db.SaveChangesAsync();
        var booking = new BookingService(db, new FixedTimeProvider(new DateTimeOffset(2026, 10, 2, 2, 0, 0, TimeSpan.Zero)));

        var created = await booking.CreateAsync(new BookingRequest(date, TimeSpan.FromHours(9), [service.ServiceId], "Khách A", "+84 900 000 002", "khach@example.com", "Gọi trước khi đến"));
        var rejected = await booking.CreateAsync(new BookingRequest(date, TimeSpan.FromHours(9), [service.ServiceId], "Khách B", "0900000003"));

        Assert.NotNull(created.Confirmation);
        Assert.Matches("^[A-Z2-9]{8}$", created.Confirmation!.Reference);
        Assert.Equal("Mai", created.Confirmation.StylistName);
        Assert.Equal(TimeSpan.FromHours(10), created.Confirmation.EndTime);
        Assert.Equal("slot_unavailable", rejected.ErrorCode);
        Assert.Single(db.Appointments);
        Assert.Equal("0900000002", db.Customers.Single().Phone);
        Assert.Equal("khach@example.com", db.Customers.Single().Email);
        Assert.Equal("Gọi trước khi đến", db.Appointments.Single().Notes);
    }

    [Fact]
    public async Task CreateBooking_RejectsInvalidCustomerFields()
    {
        await using var db = CreateDb();
        var result = await new BookingService(db, TimeProvider.System).CreateAsync(new BookingRequest(new DateTime(2026, 10, 10), TimeSpan.FromHours(9), [], "", "123456789", "not-an-email", new string('x', 301)));

        Assert.NotNull(result.FieldErrors);
        Assert.Contains("fullName", result.FieldErrors!.Keys);
        Assert.Contains("phone", result.FieldErrors.Keys);
        Assert.Contains("email", result.FieldErrors.Keys);
        Assert.Contains("notes", result.FieldErrors.Keys);
    }

    [Fact]
    public async Task CreateBooking_RejectsFourthUnfinishedAppointmentButIgnoresCancelledAndCompleted()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 10);
        var service = new Service { ServiceName = "Cắt", DurationMinutes = 30, Price = 1 }; db.Services.Add(service); await db.SaveChangesAsync();
        var stylist = new Stylist { FullName = "Mai", Phone = "0900000001", Services = [new StylistService { ServiceId = service.ServiceId }] }; var customer = new Customer { FullName = "Khách", Phone = "0900000002" }; db.AddRange(stylist, customer); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) });
        db.Appointments.AddRange(Enumerable.Range(0, 3).Select(index => new Appointment { CustomerId = customer.CustomerId, StylistId = stylist.StylistId, AppointmentDate = date.AddDays(index + 1), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9), Status = "Confirmed", BookingReference = $"TEST000{index}" })); await db.SaveChangesAsync();

        var result = await new BookingService(db, TimeProvider.System).CreateAsync(new BookingRequest(date, TimeSpan.FromHours(10), [service.ServiceId], "Khách", "+84900000002"));

        Assert.Equal("appointment_limit", result.ErrorCode);
        db.Appointments.Where(item => item.Status == "Confirmed").First().Status = "Cancelled"; await db.SaveChangesAsync();
        Assert.NotNull((await new BookingService(db, TimeProvider.System).CreateAsync(new BookingRequest(date, TimeSpan.FromHours(10), [service.ServiceId], "Khách", "0900000002"))).Confirmation);
    }

    [Fact]
    public void BookingRateLimiter_AllowsFiveBookingsThenResetsAfterOneHour()
    {
        const string ip = "203.0.113.104";
        var now = new DateTimeOffset(2026, 10, 2, 2, 0, 0, TimeSpan.Zero);
        var limiter = new BookingRateLimiter(new FixedTimeProvider(now));

        Assert.All(Enumerable.Range(0, 5), _ => Assert.True(limiter.TryReserve(ip).Allowed));
        var denied = limiter.TryReserve(ip);
        Assert.False(denied.Allowed);
        Assert.Equal(now.AddHours(1), denied.RetryAt);
        Assert.True(new BookingRateLimiter(new FixedTimeProvider(now.AddHours(1).AddMinutes(1))).TryReserve(ip).Allowed);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
