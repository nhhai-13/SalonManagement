using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/stylist-time-offs")]
public class StylistTimeOffsController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public StylistTimeOffsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // Lấy danh sách ngày nghỉ của một thợ
    [HttpGet("{stylistId:int}")]
    public async Task<IActionResult> Get(int stylistId)
    {
        var items = await _dbContext.StylistTimeOffs
            .Where(x => x.StylistId == stylistId)
            .OrderBy(x => x.OffDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync();

        return Ok(items);
    }

    // Thêm ngày/giờ nghỉ cho thợ
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] StylistTimeOffRequest request)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Yêu cầu không hợp lệ." });
        }

        // Kiểm tra thợ có tồn tại không
        var stylistExists = await _dbContext.Stylists
            .AnyAsync(x => x.StylistId == request.StylistId);

        if (!stylistExists)
        {
            return NotFound(new { message = "Không tìm thấy thợ." });
        }

        // Nếu nghỉ theo khoảng giờ
        if (!request.IsFullDay)
        {
            if (request.StartTime == null || request.EndTime == null)
            {
                return BadRequest(new { message = "Phải nhập giờ bắt đầu và giờ kết thúc." });
            }

            if (request.StartTime >= request.EndTime)
            {
                return BadRequest(new { message = "Giờ kết thúc phải sau giờ bắt đầu." });
            }
        }

        // Kiểm tra lịch nghỉ bị trùng
        var existingTimeOffs = await _dbContext.StylistTimeOffs
            .Where(x =>
                x.StylistId == request.StylistId &&
                x.OffDate == request.OffDate)
            .ToListAsync();

        // Nếu khai báo nghỉ cả ngày nhưng ngày đó đã có lịch nghỉ
        if (request.IsFullDay && existingTimeOffs.Any())
        {
            return BadRequest(new { message = "Thợ đã có lịch nghỉ trong ngày này." });
        }

        // Nếu nghỉ theo khoảng giờ
        if (!request.IsFullDay)
        {
            var isOverlap = existingTimeOffs.Any(x =>
                x.IsFullDay ||
                (x.StartTime < request.EndTime && x.EndTime > request.StartTime));

            if (isOverlap)
            {
                return BadRequest(new { message = "Khoảng giờ nghỉ bị trùng với lịch nghỉ đã có." });
            }
        }

        // Kiểm tra lịch hẹn của khách bị ảnh hưởng bởi lịch nghỉ này
        var dayStart = request.OffDate.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var appointments = await _dbContext.Appointments.AsNoTracking()
            .Where(a => a.StylistId == request.StylistId
                && a.AppointmentDate >= dayStart && a.AppointmentDate < dayEnd
                && a.Status != "Cancelled" && a.Status != "NoShow"
                && a.Status != "Rejected" && a.Status != "Completed")
            .ToListAsync();

        var affected = appointments
            .Where(a => request.IsFullDay ||
                (a.StartTime < request.EndTime!.Value.ToTimeSpan() &&
                 request.StartTime!.Value.ToTimeSpan() < a.EndTime))
            .OrderBy(a => a.StartTime)
            .Select(a => new
            {
                a.AppointmentId,
                a.BookingReference,
                Start = a.StartTime.ToString(@"hh\:mm"),
                End = a.EndTime.ToString(@"hh\:mm"),
                a.Status
            })
            .ToList();

        if (affected.Count > 0)
        {
            return Conflict(new
            {
                message = $"Lịch nghỉ này trùng với {affected.Count} lịch hẹn của khách. Vui lòng đổi hoặc hủy các lịch hẹn trước.",
                affectedAppointments = affected
            });
        }

        var timeOff = new StylistTimeOff
        {
            StylistId = request.StylistId,
            OffDate = request.OffDate,
            IsFullDay = request.IsFullDay,
            StartTime = request.IsFullDay ? null : request.StartTime,
            EndTime = request.IsFullDay ? null : request.EndTime,
            Reason = request.Reason
        };

        _dbContext.StylistTimeOffs.Add(timeOff);
        await _dbContext.SaveChangesAsync();

        return Ok(timeOff);
    }

    // Xóa lịch nghỉ
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var timeOff = await _dbContext.StylistTimeOffs.FindAsync(id);

        if (timeOff == null)
        {
            return NotFound(new { message = "Không tìm thấy lịch nghỉ." });
        }

        _dbContext.StylistTimeOffs.Remove(timeOff);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }
}

public class StylistTimeOffRequest
{
    public int StylistId { get; set; }

    public DateOnly OffDate { get; set; }

    public bool IsFullDay { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Reason { get; set; }
}