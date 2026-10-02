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

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
}
