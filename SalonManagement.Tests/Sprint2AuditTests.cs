using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
namespace SalonManagement.Tests;
[TestClass]
public class Sprint2AuditTests
{
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
    private static async Task<int[]> Seed(ApplicationDbContext db, int count = 1)
    {
        var services = Enumerable.Range(1,count).Select(i => new Service { ServiceName = "Test " + i, DurationMinutes = 30, Price = 100000, IsActive = true }).ToArray();
        db.Services.AddRange(services); await db.SaveChangesAsync();
        db.Stylists.Add(new Stylist { FullName = "Test Stylist", Phone = "0900000001", IsActive = true, Services = services.Select(s => new StylistService { ServiceId = s.ServiceId }).ToList() }); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = 1, WorkDate = new DateTime(2030,1,7), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) }); await db.SaveChangesAsync();
        return services.Select(s => s.ServiceId).ToArray();
    }
    [TestMethod]
    public void Lookup_ElevenFailuresAcrossRequests_MustBlock()
    {
        var limiter = new AppointmentLookupRateLimiter(TimeProvider.System); var ip = Guid.NewGuid().ToString();
        for (int i=0;i<11;i++) { limiter.Check(ip); limiter.RegisterFailure(ip); }
        Assert.IsTrue(limiter.Check(ip).IsBlocked, "11 failed requests should block lookup");
    }
    [TestMethod]
    public async Task Slots_ShopHoliday_MustBeEmpty()
    {
        using var db = Db(); var ids = await Seed(db);
        db.ShopHolidays.Add(new ShopHoliday { HolidayDate = new DateOnly(2030,1,7), Reason = "Tet" }); await db.SaveChangesAsync();
        var result = await new AvailabilityService(db,TimeProvider.System).GetSlotsAsync(new DateTime(2030,1,7),ids);
        Assert.AreEqual(0,result.Slots.Count,"Shop holiday must exclude all slots");
    }
    [TestMethod]
    public async Task Confirm_ShopHoliday_MustReject()
    {
        using var db = Db(); var ids = await Seed(db);
        db.ShopHolidays.Add(new ShopHoliday { HolidayDate = new DateOnly(2030,1,7), Reason = "Tet" }); await db.SaveChangesAsync();
        var result = await new AppointmentBookingService(db,TimeProvider.System).CreateAsync(new(new DateTime(2030,1,7),TimeSpan.FromHours(10),ids,"Test Customer","0912345678"));
        Assert.IsNull(result.Confirmation,"Booking on holiday must be rejected");
    }
    [TestMethod]
    public async Task Confirm_SixServices_MustReject()
    {
        using var db = Db(); var ids = await Seed(db,6);
        var result = await new AppointmentBookingService(db,TimeProvider.System).CreateAsync(new(new DateTime(2030,1,7),TimeSpan.FromHours(10),ids,"Test Customer","0912345678"));
        Assert.IsNull(result.Confirmation,"More than five services must be rejected at confirmation");
    }
    [TestMethod]
    public async Task Confirm_OutsideBusinessHours_MustReject()
    {
        using var db = Db(); var ids = await Seed(db);
        db.BusinessHours.Add(new BusinessHour { DayOfWeek = DayOfWeek.Monday, IsClosed = true }); await db.SaveChangesAsync();
        var result = await new AppointmentBookingService(db,TimeProvider.System).CreateAsync(new(new DateTime(2030,1,7),TimeSpan.FromHours(10),ids,"Test Customer","0912345678"));
        Assert.IsNull(result.Confirmation,"Booking on closed business day must be rejected");
    }
    [TestMethod]
    public async Task Slots_StylistPartialTimeOff_ExcludesOverlapAndKeepsBoundary()
    {
        using var db = Db(); var ids = await Seed(db);
        db.StylistTimeOffs.Add(new StylistTimeOff { StylistId = 1, OffDate = new DateOnly(2030,1,7), IsFullDay = false, StartTime = new TimeOnly(10,0), EndTime = new TimeOnly(11,0) });
        await db.SaveChangesAsync();
        var result = await new AvailabilityService(db,TimeProvider.System).GetSlotsAsync(new DateTime(2030,1,7),ids);
        Assert.IsFalse(result.Slots.Contains(TimeSpan.FromHours(10)));
        Assert.IsFalse(result.Slots.Contains(new TimeSpan(9,45,0)));
        Assert.IsTrue(result.Slots.Contains(new TimeSpan(9,30,0)));
        Assert.IsTrue(result.Slots.Contains(TimeSpan.FromHours(11)));
    }
    [TestMethod]
    public async Task Confirm_StylistFullDayOff_MustReject()
    {
        using var db = Db(); var ids = await Seed(db);
        db.StylistTimeOffs.Add(new StylistTimeOff { StylistId = 1, OffDate = new DateOnly(2030,1,7), IsFullDay = true }); await db.SaveChangesAsync();
        var result = await new AppointmentBookingService(db,TimeProvider.System).CreateAsync(new(new DateTime(2030,1,7),TimeSpan.FromHours(10),ids,"Test Customer","0912345678"));
        Assert.IsNull(result.Confirmation);
    }
    [TestMethod]
    public async Task Confirm_LeadTimeBoundary_Rejects59AndAllows60Minutes()
    {
        using var db = Db(); var ids = await Seed(db);
        var clock = new AuditClock(new DateTimeOffset(2030,1,7,2,0,0,TimeSpan.Zero)); // 09:00 Vietnam
        var booking = new AppointmentBookingService(db,clock);
        var early = await booking.CreateAsync(new(new DateTime(2030,1,7),new TimeSpan(9,59,0),ids,"Test Customer","0912345678"));
        Assert.IsNull(early.Confirmation);
        var valid = await booking.CreateAsync(new(new DateTime(2030,1,7),TimeSpan.FromHours(10),ids,"Test Customer","0912345678"));
        Assert.IsNotNull(valid.Confirmation);
    }
    [TestMethod]
    public async Task Confirm_SpecificStylist_PreservesSelection()
    {
        using var db = Db(); var ids = await Seed(db);
        db.Stylists.Add(new Stylist { FullName = "Second stylist", Phone = "0900000002", IsActive = true, Services = [new StylistService { ServiceId = ids[0] }] }); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = 2, WorkDate = new DateTime(2030,1,7), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) }); await db.SaveChangesAsync();
        var result = await new AppointmentBookingService(db,TimeProvider.System).CreateAsync(new(new DateTime(2030,1,7),TimeSpan.FromHours(10),ids,"Test Customer","0912345678",StylistId:2));
        Assert.IsNotNull(result.Confirmation);
        Assert.AreEqual(2,(await db.Appointments.SingleAsync()).StylistId);
    }
    [TestMethod]
    public async Task Confirm_SpecificStylistWithoutSkills_DoesNotSubstituteAnother()
    {
        using var db = Db(); var ids = await Seed(db);
        db.Stylists.Add(new Stylist { FullName = "Unqualified", Phone = "0900000002", IsActive = true }); await db.SaveChangesAsync();
        var result = await new AppointmentBookingService(db,TimeProvider.System).CreateAsync(new(new DateTime(2030,1,7),TimeSpan.FromHours(10),ids,"Test Customer","0912345678",StylistId:2));
        Assert.IsNull(result.Confirmation);
        Assert.AreEqual(0,await db.Appointments.CountAsync());
    }
    [TestMethod]
    public async Task Confirm_AnyStylist_PrefersLeastBusy()
    {
        using var db = Db(); var ids = await Seed(db);
        db.Stylists.Add(new Stylist { FullName = "Second stylist", Phone = "0900000002", IsActive = true, Services = [new StylistService { ServiceId = ids[0] }] }); await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = 2, WorkDate = new DateTime(2030,1,7), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17) });
        db.Appointments.Add(new Appointment { StylistId = 1, AppointmentDate = new DateTime(2030,1,7), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(9), Status = "Confirmed", BookingReference = "TEST0001", Customer = new Customer { FullName = "Existing", Phone = "0900000003" } }); await db.SaveChangesAsync();
        var result = await new AppointmentBookingService(db,TimeProvider.System).CreateAsync(new(new DateTime(2030,1,7),TimeSpan.FromHours(10),ids,"Test Customer","0912345678",StylistId:0));
        Assert.IsNotNull(result.Confirmation);
        Assert.AreEqual("Second stylist",result.Confirmation.StylistName);
    }
    private sealed class AuditClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
