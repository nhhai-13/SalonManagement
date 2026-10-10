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
    public async Task<IActionResult> Create(ShopHolidayRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Vui lòng nhập lý do nghỉ." });
        if (request.ToDate < request.FromDate)
            return BadRequest(new { message = "Ngày kết thúc phải từ ngày bắt đầu trở đi." });

        var dates = Enumerable.Range(0, request.ToDate.DayNumber - request.FromDate.DayNumber + 1)
            .Select(offset => request.FromDate.AddDays(offset))
            .ToList();
        var existing = await _dbContext.ShopHolidays
            .Where(holiday => dates.Contains(holiday.HolidayDate))
            .Select(holiday => holiday.HolidayDate)
            .ToListAsync();

        if (existing.Count > 0)
        {
            return BadRequest(new
            {
                message = $"Đã có ngày nghỉ trong khoảng đã chọn: {string.Join(", ", existing.Select(date => date.ToString("dd/MM/yyyy")))}."
            });
        }

        var start = request.FromDate.ToDateTime(TimeOnly.MinValue);
        var end = request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var affectedAppointments = await _dbContext.Appointments.AsNoTracking()
            .Where(appointment => appointment.AppointmentDate >= start && appointment.AppointmentDate < end && appointment.Status != "Cancelled")
            .OrderBy(appointment => appointment.AppointmentDate).ThenBy(appointment => appointment.StartTime)
            .Select(appointment => new { appointment.AppointmentId, appointment.AppointmentDate, appointment.StartTime, appointment.EndTime, appointment.Status })
            .ToListAsync();
        if (affectedAppointments.Count > 0)
            return Conflict(new { message = "Khoảng ngày nghỉ trùng với lịch hẹn đã có.", affectedAppointments });

        var holidays = dates.Select(date => new ShopHoliday { HolidayDate = date, Reason = request.Reason.Trim(), CreatedAt = DateTime.UtcNow }).ToList();
        _dbContext.ShopHolidays.AddRange(holidays);
        await _dbContext.SaveChangesAsync();

        return Ok(holidays);
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

public sealed record ShopHolidayRequest(DateOnly FromDate, DateOnly ToDate, string? Reason);
