using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/stylist-time-offs")]
public class StylistTimeOffsController(ApplicationDbContext dbContext) : ControllerBase
{
    // Lấy danh sách ngày nghỉ của một thợ
    [HttpGet("{stylistId:int}")]
    public async Task<IActionResult> Get(int stylistId)
    {
        var items = await dbContext.StylistTimeOffs
            .Where(x => x.StylistId == stylistId)
            .OrderBy(x => x.OffDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync();

        return Ok(items);
    }

    // Thêm ngày/giờ nghỉ cho thợ
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] StylistTimeOffRequest request)
    {
        // Kiểm tra thợ có tồn tại không
        var stylistExists = await dbContext.Stylists
            .AnyAsync(x => x.StylistId == request.StylistId);

        if (!stylistExists)
        {
            return NotFound(new
            {
                message = "Không tìm thấy thợ."
            });
        }

        // Nếu nghỉ theo khoảng giờ
        if (!request.IsFullDay)
        {
            if (request.StartTime == null || request.EndTime == null)
            {
                return BadRequest(new
                {
                    message = "Phải nhập giờ bắt đầu và giờ kết thúc."
                });
            }

            if (request.StartTime >= request.EndTime)
            {
                return BadRequest(new
                {
                    message = "Giờ kết thúc phải sau giờ bắt đầu."
                });
            }
        }
        // Kiểm tra lịch nghỉ bị trùng
        var existingTimeOffs = await dbContext.StylistTimeOffs
            .Where(x =>
                x.StylistId == request.StylistId &&
                x.OffDate == request.OffDate)
            .ToListAsync();

        // Nếu khai báo nghỉ cả ngày nhưng ngày đó đã có lịch nghỉ
        if (request.IsFullDay && existingTimeOffs.Any())
        {
            return BadRequest(new
            {
                message = "Thợ đã có lịch nghỉ trong ngày này."
         });
        }

       // Nếu nghỉ theo khoảng giờ
       if (!request.IsFullDay)
       {
           var isOverlap = existingTimeOffs.Any(x =>
               x.IsFullDay ||
              (x.StartTime < request.EndTime &&
               x.EndTime > request.StartTime));

    if (isOverlap)
    {
        return BadRequest(new
        {
            message = "Khoảng giờ nghỉ bị trùng với lịch nghỉ đã có."
        });
    }
}
        // Kiểm tra các lịch hẹn bị ảnh hưởng bởi ngày/giờ nghỉ
        var appointmentDay = request.OffDate.ToDateTime(TimeOnly.MinValue);
        var nextDay = appointmentDay.AddDays(1);

        var affectedAppointments = await dbContext.Appointments
            .AsNoTracking()
            .Where(a =>
                a.StylistId == request.StylistId &&
                a.AppointmentDate >= appointmentDay &&
                a.AppointmentDate < nextDay &&
                a.Status != "Cancelled")
            .Where(a =>
                request.IsFullDay ||
                (request.StartTime.HasValue &&
                request.EndTime.HasValue &&
                a.StartTime < request.EndTime.Value.ToTimeSpan() &&
                a.EndTime > request.StartTime.Value.ToTimeSpan()))
            .OrderBy(a => a.StartTime)
            .Select(a => new
            {
                a.AppointmentId,
                a.AppointmentDate,
                a.StartTime,
                a.EndTime,
                a.Status
           })
            .ToListAsync();

   if (affectedAppointments.Count > 0)
   {
    return Conflict(new
    {
        message = "Thời gian nghỉ trùng với lịch hẹn đã có.",
        affectedAppointments
    });
}
        var timeOff = new StylistTimeOff    
        {
            StylistId = request.StylistId,
            OffDate = request.OffDate,
            IsFullDay = request.IsFullDay,

            StartTime = request.IsFullDay
                ? null
                : request.StartTime,

            EndTime = request.IsFullDay
                ? null
                : request.EndTime,

            Reason = request.Reason
        };

        dbContext.StylistTimeOffs.Add(timeOff);
        await dbContext.SaveChangesAsync();

        return Ok(timeOff);
    }

    // Xóa lịch nghỉ
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var timeOff = await dbContext.StylistTimeOffs
            .FindAsync(id);

        if (timeOff == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy lịch nghỉ."
            });
        }

        dbContext.StylistTimeOffs.Remove(timeOff);
        await dbContext.SaveChangesAsync();

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