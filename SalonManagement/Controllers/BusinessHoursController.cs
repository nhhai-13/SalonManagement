using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[Authorize(Roles = RoleGroups.Management)]
[ApiController]
[Route("api/business-hours")]
public class BusinessHoursController(ApplicationDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BusinessHourResponse>>> Get()
    {
        var stored = await dbContext.BusinessHours
            .ToDictionaryAsync(hours => hours.DayOfWeek);

        return Ok(Enum.GetValues<DayOfWeek>().Select(day =>
        {
            var hours = stored.GetValueOrDefault(day);

            return new BusinessHourResponse(
                day,
                hours?.IsClosed ?? false,
                hours?.OpensAt ?? new TimeOnly(8, 0),
                hours?.ClosesAt ?? new TimeOnly(17, 0),
                BusinessHour.SalonTimeZone);
        }).OrderBy(item =>
            item.DayOfWeek == DayOfWeek.Sunday
                ? 7
                : (int)item.DayOfWeek));
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateBusinessHoursRequest request)
    {
        if (request.Days
                .Select(day => day.DayOfWeek)
                .Distinct()
                .Count() != 7 ||
            request.Days.Any(day => day.DayOfWeek is < 0 or > 6))
        {
            ModelState.AddModelError(
                nameof(request.Days),
                "Cấu hình phải chứa đúng 7 ngày, không trùng lặp.");
        }

        foreach (var day in request.Days.Where(day => !day.IsClosed))
        {
            if (day.OpensAt is null || day.ClosesAt is null)
            {
                ModelState.AddModelError(
                    $"Days[{day.DayOfWeek}]",
                    "Ngày mở cửa phải có giờ mở và giờ đóng.");
            }
            else if (
                day.ClosesAt.Value.ToTimeSpan() -
                day.OpensAt.Value.ToTimeSpan()
                < TimeSpan.FromMinutes(60))
            {
                ModelState.AddModelError(
                    $"Days[{day.DayOfWeek}]",
                    "Giờ đóng phải sau giờ mở ít nhất 60 phút.");
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var stored = await dbContext.BusinessHours
            .ToDictionaryAsync(hours => hours.DayOfWeek);

        foreach (var input in request.Days)
        {
            var day = (DayOfWeek)input.DayOfWeek;

            if (!stored.TryGetValue(day, out var hours))
            {
                hours = new BusinessHour
                {
                    DayOfWeek = day
                };

                dbContext.BusinessHours.Add(hours);
            }

            hours.IsClosed = input.IsClosed;
            hours.OpensAt = input.IsClosed ? null : input.OpensAt;
            hours.ClosesAt = input.IsClosed ? null : input.ClosesAt;
            hours.TimeZoneId = BusinessHour.SalonTimeZone;
        }

        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}