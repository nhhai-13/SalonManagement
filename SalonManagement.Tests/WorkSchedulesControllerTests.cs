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
    [InlineData(9, 11)]  // Nằm hoàn toàn trong ca đã có.
    [InlineData(7, 13)]  // Bao phủ ca đã có.
    [InlineData(7, 9)]   // Chồng phần đầu ca đã có.
    [InlineData(11, 13)] // Chồng phần cuối ca đã có.
    public async Task Create_OverlappingShift_IsRejectedAndNamesConflictingShift(int startHour, int endHour)
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Hoàng Dung"); var controller = CreateController(db); var date = new DateTime(2026, 10, 8);
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) });
        await db.SaveChangesAsync();

        await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(startHour), EndTime = TimeSpan.FromHours(endHour) });

        Assert.Single(await db.WorkSchedules.ToListAsync());
        Assert.Contains(controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage.Contains("08:00–12:00"));
    }

    [Fact]
    public async Task Create_AdjacentShifts_AreAllowed()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Phạm Giang"); var controller = CreateController(db); var date = new DateTime(2026, 10, 8);
        await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) });
        await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(12), EndTime = TimeSpan.FromHours(16) });
        Assert.Equal(2, await db.WorkSchedules.CountAsync());
    }

    [Fact]
    public async Task Edit_OverlappingShift_IsRejectedAndKeepsExistingData()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Vũ Hân"); var controller = CreateController(db); var date = new DateTime(2026, 10, 8);
        var first = new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12), Notes = "Ca sáng" };
        var second = new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(13), EndTime = TimeSpan.FromHours(17), Notes = "Ca chiều" };
        db.WorkSchedules.AddRange(first, second); await db.SaveChangesAsync();

        await controller.Edit(second.WorkScheduleId, new EditWorkScheduleViewModel { WorkScheduleId = second.WorkScheduleId, StartTime = TimeSpan.FromHours(11), EndTime = TimeSpan.FromHours(15), Notes = "Ghi chú mới" });

        var unchanged = await db.WorkSchedules.SingleAsync(schedule => schedule.WorkScheduleId == second.WorkScheduleId);
        Assert.Equal(TimeSpan.FromHours(13), unchanged.StartTime); Assert.Equal(TimeSpan.FromHours(17), unchanged.EndTime); Assert.Equal("Ca chiều", unchanged.Notes);
        Assert.Contains(controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage.Contains("08:00–12:00"));
    }

    [Fact]
    public async Task Edit_NonOverlappingShift_UpdatesTimesAndBreakNote()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Ngô Lan"); var controller = CreateController(db); var date = new DateTime(2026, 10, 8);
        var schedule = new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12), Notes = "Cũ" };
        db.WorkSchedules.Add(schedule); await db.SaveChangesAsync();

        await controller.Edit(schedule.WorkScheduleId, new EditWorkScheduleViewModel { WorkScheduleId = schedule.WorkScheduleId, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(13), Notes = "Nghỉ 11:00 - 11:15" });

        var updated = await db.WorkSchedules.SingleAsync();
        Assert.Equal(TimeSpan.FromHours(9), updated.StartTime); Assert.Equal(TimeSpan.FromHours(13), updated.EndTime); Assert.Equal("Nghỉ 11:00 - 11:15", updated.Notes);
    }

    [Theory]
    [InlineData(8, 0, 17, 0)]
    [InlineData(8, 0, 12, 0)]
    [InlineData(9, 0, 17, 0)]
    public async Task Create_ShiftWithinBusinessHours_IncludingBoundaries_IsAllowed(int startHour, int startMinute, int endHour, int endMinute)
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Đinh Mai"); var controller = CreateController(db); var date = new DateTime(2026, 10, 8);
        await AddBusinessHoursAsync(db, date.DayOfWeek, false, new TimeOnly(8, 0), new TimeOnly(17, 0));

        await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = new TimeSpan(startHour, startMinute, 0), EndTime = new TimeSpan(endHour, endMinute, 0) });

        Assert.Single(await db.WorkSchedules.ToListAsync());
    }

    [Theory]
    [InlineData(7, 30, 16, 0)]
    [InlineData(9, 0, 17, 30)]
    public async Task Create_ShiftOutsideBusinessHours_IsRejectedWithAllowedRange(int startHour, int startMinute, int endHour, int endMinute)
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Đặng Ngân"); var controller = CreateController(db); var date = new DateTime(2026, 10, 8);
        await AddBusinessHoursAsync(db, date.DayOfWeek, false, new TimeOnly(8, 0), new TimeOnly(17, 0));

        await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = new TimeSpan(startHour, startMinute, 0), EndTime = new TimeSpan(endHour, endMinute, 0) });

        Assert.Empty(await db.WorkSchedules.ToListAsync());
        Assert.Contains(controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage.Contains("08:00–17:00"));
    }

    [Fact]
    public async Task Create_ShiftOnClosedDay_IsRejected()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Mai Yến"); var controller = CreateController(db); var date = new DateTime(2026, 10, 11);
        await AddBusinessHoursAsync(db, date.DayOfWeek, true, null, null);

        await controller.Create(new CreateWorkScheduleViewModel { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(12) });

        Assert.Empty(await db.WorkSchedules.ToListAsync());
        Assert.Contains(controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage.Contains("không hoạt động"));
    }

    [Fact]
    public async Task Edit_ShiftOutsideBusinessHours_IsRejectedAndKeepsExistingData()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Bảo Trâm"); var controller = CreateController(db); var date = new DateTime(2026, 10, 8);
        await AddBusinessHoursAsync(db, date.DayOfWeek, false, new TimeOnly(8, 0), new TimeOnly(17, 0));
        var schedule = new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(12), Notes = "Giữ nguyên" };
        db.WorkSchedules.Add(schedule); await db.SaveChangesAsync();

        await controller.Edit(schedule.WorkScheduleId, new EditWorkScheduleViewModel { WorkScheduleId = schedule.WorkScheduleId, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(18), Notes = "Không được lưu" });

        var unchanged = await db.WorkSchedules.SingleAsync();
        Assert.Equal(TimeSpan.FromHours(9), unchanged.StartTime); Assert.Equal(TimeSpan.FromHours(12), unchanged.EndTime); Assert.Equal("Giữ nguyên", unchanged.Notes);
    }

    [Fact]
    public async Task Index_ShowsBusinessHoursForEachDay()
    {
        await using var db = CreateDb(); var date = new DateTime(2026, 10, 8);
        await AddBusinessHoursAsync(db, date.DayOfWeek, false, new TimeOnly(10, 0), new TimeOnly(19, 0));

        var result = await CreateController(db).Index(null, date);
        var model = Assert.IsType<ViewResult>(result).Model as WeeklyWorkScheduleViewModel;

        Assert.NotNull(model); Assert.Equal(new TimeOnly(10, 0), model.BusinessHours[date.DayOfWeek].OpensAt); Assert.Equal(new TimeOnly(19, 0), model.BusinessHours[date.DayOfWeek].ClosesAt);
    }

    [Fact]
    public async Task CopyWeek_EmptyTarget_CopiesDatesTimesAndNotes()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Tú Anh"); var controller = CreateController(db); var sourceWeek = new DateTime(2026, 10, 5);
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12), Notes = "Nghỉ 10:00" },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek.AddDays(2), StartTime = TimeSpan.FromHours(13), EndTime = TimeSpan.FromHours(17), Notes = "Nghỉ 15:00" });
        await db.SaveChangesAsync();

        var result = await controller.CopyWeek(new CopyWorkWeekViewModel { StylistId = stylist.StylistId, SourceWeekStart = sourceWeek, ConflictResolution = CopyConflictResolution.Skip });

        Assert.IsType<RedirectToActionResult>(result);
        var copies = await db.WorkSchedules.Where(schedule => schedule.WorkDate >= sourceWeek.AddDays(7)).OrderBy(schedule => schedule.WorkDate).ToListAsync();
        Assert.Equal(2, copies.Count);
        Assert.Equal(sourceWeek.AddDays(7), copies[0].WorkDate); Assert.Equal(TimeSpan.FromHours(8), copies[0].StartTime); Assert.Equal("Nghỉ 10:00", copies[0].Notes);
        Assert.Equal(sourceWeek.AddDays(9), copies[1].WorkDate); Assert.Equal(TimeSpan.FromHours(17), copies[1].EndTime); Assert.Equal("Nghỉ 15:00", copies[1].Notes);
    }

    [Fact]
    public async Task CopyWeek_ExistingTargetWithSkip_KeepsExistingShiftAndDoesNotDuplicate()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Hà My"); var controller = CreateController(db); var sourceWeek = new DateTime(2026, 10, 5);
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12), Notes = "Nguồn" },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek.AddDays(7), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(11), Notes = "Có sẵn" });
        await db.SaveChangesAsync();

        await controller.CopyWeek(new CopyWorkWeekViewModel { StylistId = stylist.StylistId, SourceWeekStart = sourceWeek, ConflictResolution = CopyConflictResolution.Skip });

        var target = await db.WorkSchedules.Where(schedule => schedule.WorkDate == sourceWeek.AddDays(7)).ToListAsync();
        Assert.Single(target); Assert.Equal("Có sẵn", target[0].Notes);
        Assert.Contains("bỏ qua 1 ngày", controller.TempData["Success"]?.ToString());
    }

    [Fact]
    public async Task CopyWeek_ExistingTargetWithOverwrite_ReplacesTargetShifts()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Quỳnh Như"); var controller = CreateController(db); var sourceWeek = new DateTime(2026, 10, 5);
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12), Notes = "Nguồn sáng" },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek, StartTime = TimeSpan.FromHours(13), EndTime = TimeSpan.FromHours(17), Notes = "Nguồn chiều" },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek.AddDays(7), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(11), Notes = "Ca cũ" });
        await db.SaveChangesAsync();

        await controller.CopyWeek(new CopyWorkWeekViewModel { StylistId = stylist.StylistId, SourceWeekStart = sourceWeek, ConflictResolution = CopyConflictResolution.Overwrite });

        var target = await db.WorkSchedules.Where(schedule => schedule.WorkDate == sourceWeek.AddDays(7)).OrderBy(schedule => schedule.StartTime).ToListAsync();
        Assert.Equal(2, target.Count); Assert.DoesNotContain(target, schedule => schedule.Notes == "Ca cũ"); Assert.Equal("Nguồn sáng", target[0].Notes); Assert.Equal("Nguồn chiều", target[1].Notes);
        Assert.Contains("ghi đè 1 ngày", controller.TempData["Success"]?.ToString());
    }

    [Fact]
    public async Task CopyPreview_ReportsOnlyTargetDaysThatAlreadyHaveShifts()
    {
        await using var db = CreateDb(); var stylist = await AddStylistAsync(db, "Khánh Linh"); var sourceWeek = new DateTime(2026, 10, 5);
        db.WorkSchedules.AddRange(
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek.AddDays(1), StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) },
            new WorkSchedule { StylistId = stylist.StylistId, WorkDate = sourceWeek.AddDays(7), StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(11) });
        await db.SaveChangesAsync();

        var result = await CreateController(db).CopyPreview(stylist.StylistId, sourceWeek);
        var preview = Assert.IsType<OkObjectResult>(result.Result).Value as CopyWeekPreviewResponse;

        Assert.NotNull(preview); Assert.Equal(sourceWeek.AddDays(7), preview.TargetWeekStart); Assert.Single(preview.ConflictDays); Assert.Equal(sourceWeek.AddDays(7), preview.ConflictDays[0].Date); Assert.Equal(1, preview.ConflictDays[0].ExistingShiftCount);
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
    private static async Task AddBusinessHoursAsync(ApplicationDbContext db, DayOfWeek day, bool isClosed, TimeOnly? opensAt, TimeOnly? closesAt)
    {
        db.BusinessHours.Add(new BusinessHour { DayOfWeek = day, IsClosed = isClosed, OpensAt = opensAt, ClosesAt = closesAt }); await db.SaveChangesAsync();
    }
    private static WorkSchedulesController CreateController(ApplicationDbContext db) => new(db) { TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider()) };
    private sealed class TestTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
