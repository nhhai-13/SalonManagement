using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using Xunit;

namespace SalonManagement.Tests;

public sealed class WorkSchedulesControllerTests
{
    [Fact]
    public void Controller_AllowsOnlyOwnerRole()
    {
        var authorize = Assert.Single(typeof(WorkSchedulesController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(UserRoles.Owner, authorize.Roles);
    }

    [Fact]
    public async Task Create_ValidShift_PersistsBreakNote()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Nguyễn An"); var controller = CreateController(db); var date = new DateTime(2026, 10, 7);
        var result = await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12), Notes = "Nghỉ 10:00 - 10:15" });
        Assert.IsType<RedirectToActionResult>(result);
        var schedule = Assert.Single(await db.WorkSchedules.ToListAsync());
        Assert.Equal(date, schedule.WorkDate); Assert.Equal("Nghỉ 10:00 - 10:15", schedule.Notes);
    }

    [Fact]
    public async Task Create_MultipleShiftsOnSameDay_PersistsBoth()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Trần Bình"); var controller = CreateController(db); var date = new DateTime(2026, 10, 8);
        await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) });
        await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(13), EndTime = TimeSpan.FromHours(17) });
        Assert.Equal(2, await db.WorkSchedules.CountAsync(schedule => schedule.StylistId == stylist.StylistId && schedule.WorkDate == date));
    }

    [Theory]
    [InlineData(9, 9)]
    [InlineData(10, 9)]
    public async Task Create_StartTimeEqualOrAfterEndTime_DoesNotPersist(int startHour, int endHour)
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Lê Chi"); var controller = CreateController(db);
        var result = await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = new DateTime(2026, 10, 9), StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour) });
        Assert.IsType<RedirectToActionResult>(result); Assert.Empty(await db.WorkSchedules.ToListAsync());
        Assert.True(controller.ModelState.ContainsKey(nameof(CreateWorkScheduleViewModel.StartTime)));
    }

    [Fact]
    public async Task Index_ShowsOnlySelectedStylistAndRequestedWeek()
    {
        await using var db = CreateDb(); var first = await AddStylistAsync(db, "An"); var second = await AddStylistAsync(db, "Bình");
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = first.StylistId, WorkDate = new DateTime(2026, 10, 5), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) },
            new WorkSchedule { StylistId = second.StylistId, WorkDate = new DateTime(2026, 10, 5), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) },
            new WorkSchedule { StylistId = first.StylistId, WorkDate = new DateTime(2026, 10, 12), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) });
        await db.SaveChangesAsync();
        var result = await CreateController(db).Index(first.StylistId, new DateTime(2026, 10, 7));
        var model = Assert.IsType<ViewResult>(result).Model as WeeklyWorkScheduleViewModel;
        Assert.NotNull(model); Assert.Equal(new DateTime(2026, 10, 5), model.WeekStart); Assert.Single(model.Schedules); Assert.Equal(first.StylistId, model.Schedules[0].StylistId);
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
    private static async Task<Stylist> AddStylistAsync(ApplicationDbContext db, string name)
    {
        var stylist = new Stylist { FullName = name, Phone = $"09{Random.Shared.Next(10000000, 99999999)}" }; db.Stylists.Add(stylist); await db.SaveChangesAsync(); return stylist;
    }
    private static WorkSchedulesController CreateController(ApplicationDbContext db) => new(db) { TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider()) };
    private sealed class TestTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
