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