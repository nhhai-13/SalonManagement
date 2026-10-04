using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/shop-holidays")]
public class ShopHolidaysController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public ShopHolidaysController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // Lấy danh sách ngày nghỉ
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var holidays = await _dbContext.ShopHolidays
            .OrderBy(h => h.HolidayDate)
            .ToListAsync();

        return Ok(holidays);
    }

    // Thêm ngày nghỉ mới
    [HttpPost]
    public async Task<IActionResult> Create(ShopHoliday request)
    {
        var exists = await _dbContext.ShopHolidays
            .AnyAsync(h => h.HolidayDate == request.HolidayDate);

        if (exists)
        {
            return BadRequest(new
            {
                message = "Ngày này đã được thiết lập là ngày nghỉ."
            });
        }

        var day = request.HolidayDate.ToDateTime(TimeOnly.MinValue);
        var nextDay = day.AddDays(1);
        var affected = await _dbContext.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= day && a.AppointmentDate < nextDay &&
                (a.Status == "Pending" || a.Status == "Confirmed" || a.Status == "InProgress"))
            .Select(a => new { a.AppointmentId, a.BookingReference, a.StartTime, a.EndTime }).ToListAsync();
        if (affected.Count > 0)
            return Conflict(new { message = "Ngày nghỉ trùng với lịch hẹn đang hoạt động. Vui lòng xử lý lịch hẹn trước khi tạo ngày nghỉ.", affectedAppointments = affected });
        var holiday = new ShopHoliday
        {
            HolidayDate = request.HolidayDate,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ShopHolidays.Add(holiday);
        await _dbContext.SaveChangesAsync();

        return Ok(holiday);
    }

    // Tạo khoảng ngày nghỉ sau khi xác nhận các lịch bị ảnh hưởng.
    [HttpPost("range")]
    public async Task<IActionResult> CreateRange(ShopHolidayRangeRequest request)
    {
        if (request.StartDate == default || request.EndDate < request.StartDate || request.EndDate == DateOnly.MaxValue ||
            request.EndDate.DayNumber - request.StartDate.DayNumber > 365 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
            return BadRequest(new { message = "Chọn khoảng ngày hợp lệ (tối đa 366 ngày) và lý do tối đa 500 ký tự." });
        if (await _dbContext.ShopHolidays.AnyAsync(h => h.HolidayDate >= request.StartDate && h.HolidayDate <= request.EndDate))
            return Conflict(new { message = "Khoảng ngày có ngày nghỉ đã được thiết lập." });
        var start = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var end = request.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var affected = await _dbContext.Appointments.AsNoTracking().Where(a => a.AppointmentDate >= start && a.AppointmentDate < end &&
            (a.Status == "Pending" || a.Status == "Confirmed" || a.Status == "InProgress"))
            .Select(a => new { a.AppointmentId, a.BookingReference, a.AppointmentDate, a.StartTime, a.EndTime }).ToListAsync();
        if (affected.Count > 0 && !request.ConfirmAffectedAppointments)
            return Conflict(new { message = "Khoảng nghỉ ảnh hưởng lịch hẹn. Kiểm tra danh sách trước khi xác nhận lưu.", affectedCount = affected.Count, affectedAppointments = affected });
        var holidays = Enumerable.Range(0, request.EndDate.DayNumber - request.StartDate.DayNumber + 1)
            .Select(i => new ShopHoliday { HolidayDate = request.StartDate.AddDays(i), Reason = request.Reason.Trim(), CreatedAt = DateTime.UtcNow }).ToList();
        _dbContext.ShopHolidays.AddRange(holidays);
        await _dbContext.SaveChangesAsync();
        return Ok(new { holidays, affectedCount = affected.Count, affectedAppointments = affected });
    }

    // Xóa ngày nghỉ
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var holiday = await _dbContext.ShopHolidays
            .FirstOrDefaultAsync(h => h.ShopHolidayId == id);

        if (holiday == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy ngày nghỉ."
            });
        }

        _dbContext.ShopHolidays.Remove(holiday);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
}

public sealed record ShopHolidayRangeRequest(DateOnly StartDate, DateOnly EndDate, string Reason, bool ConfirmAffectedAppointments = false);
