using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public class BusinessHoursControllerTests
{
    [Fact]
    public async Task Update_RejectsOnlyTheDayWithLessThanSixtyMinutes()
    {
        await using var db = CreateDb();
        var controller = new BusinessHoursController(db);
        var days = ValidWeek().ToArray();
        days[1] = days[1] with { OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(8, 59) };

        var result = await controller.Update(new UpdateBusinessHoursRequest(days));

        Assert.IsType<ObjectResult>(result);
        Assert.Contains("Days[1]", controller.ModelState.Keys);
        Assert.Empty(db.BusinessHours);
    }

    [Fact]
    public async Task Update_AllowsClosedDaysWithoutTimes_AndPersistsTimeZone()
    {
        await using var db = CreateDb();
        var controller = new BusinessHoursController(db);
        var days = ValidWeek().ToArray();
        days[0] = days[0] with { IsClosed = true, OpensAt = null, ClosesAt = null };

        var result = await controller.Update(new UpdateBusinessHoursRequest(days));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(7, await db.BusinessHours.CountAsync());
        var sunday = await db.BusinessHours.SingleAsync(day => day.DayOfWeek == DayOfWeek.Sunday);
        Assert.True(sunday.IsClosed);
        Assert.Null(sunday.OpensAt);
        Assert.Equal(BusinessHour.SalonTimeZone, sunday.TimeZoneId);
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(9, 1)]
    [InlineData(21, 0)]
    public async Task Update_AcceptsAtLeastSixtyMinutes(int closingHour, int closingMinute)
    {
        await using var db = CreateDb();
        var controller = new BusinessHoursController(db);
        var days = ValidWeek().Select(day => day with { ClosesAt = new TimeOnly(closingHour, closingMinute) }).ToArray();

        var result = await controller.Update(new UpdateBusinessHoursRequest(days));

        Assert.IsType<NoContentResult>(result);
    }

    private static IEnumerable<UpdateBusinessHourRequest> ValidWeek() =>
        Enumerable.Range(0, 7).Select(day => new UpdateBusinessHourRequest(day, false, new TimeOnly(8, 0), new TimeOnly(17, 0)));

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options);
    }
}
