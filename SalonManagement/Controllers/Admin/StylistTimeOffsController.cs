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
 feature/s4-02/recalculate-discount-on-service-change-and-reports
 feature/s4-02/recalculate-discount-on-service-change-and-reports

 feature/s2-02-stylist-time-off-warning
    feature/s2-02-stylist-time-off-warning

 feature/s2-02-stylist-time-off-booking
 develop

 develop
        if (string.IsNullOrWhiteSpace(request.Reason))

        if (request is null)
 develop
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

 feature/s4-02/recalculate-discount-on-service-change-and-reports
       // Nếu nghỉ theo khoảng giờ
       if (!request.IsFullDay)
       {
           var isOverlap = existingTimeOffs.Any(x =>
               x.IsFullDay ||
              (x.StartTime < request.EndTime &&
               x.EndTime > request.StartTime));

 feature/s4-02/recalculate-discount-on-service-change-and-reports
            if (isOverlap)
            {
                return BadRequest(new
                {
                    message = "Khoảng giờ nghỉ bị trùng với lịch nghỉ đã có."
                });
            }
        }

    if (isOverlap)
    {
        return BadRequest(new
        {
            message = "Khoảng giờ nghỉ bị trùng với lịch nghỉ đã có."
        });
    }
}
 feature/s2-02-stylist-time-off-warning
 feature/s2-02-stylist-time-off-warning
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

 feature/s2-02-stylist-time-off-booking
 develop
 develop

        var appointmentDay = request.OffDate.ToDateTime(TimeOnly.MinValue);
    var nextDay = appointmentDay.AddDays(1);
    var affectedAppointments = await dbContext.Appointments
        .Include(appointment => appointment.AppointmentServices)
        .Where(appointment => appointment.StylistId == request.StylistId &&
            appointment.AppointmentDate >= appointmentDay && appointment.AppointmentDate < nextDay &&
            appointment.Status != "Cancelled")
        .Where(appointment => request.IsFullDay ||
            (request.StartTime.HasValue && request.EndTime.HasValue &&
             appointment.StartTime < request.EndTime.Value.ToTimeSpan() &&
             appointment.EndTime > request.StartTime.Value.ToTimeSpan()))
        .OrderBy(appointment => appointment.StartTime)
        .ToListAsync();

    if (affectedAppointments.Count > 0)
    {
        if (!request.ReplacementStylistId.HasValue)
        {
            return Conflict(new
            {
                message = "Thời gian nghỉ trùng với lịch hẹn đã có. Vui lòng chọn người thay thế để chuyển lịch.",
                affectedAppointments = affectedAppointments.Select(appointment => new
                {
                    appointment.AppointmentId,
                    appointment.AppointmentDate,
                    appointment.StartTime,
                    appointment.EndTime,
                    appointment.Status
                })
            });
        }

 feature/s4-02/recalculate-discount-on-service-change-and-reports
        var replacementError = await ValidateReplacementAsync(request.ReplacementStylistId.Value, request.StylistId,
            request.OffDate, affectedAppointments);
        if (replacementError is not null)
            return BadRequest(new { message = replacementError });

        foreach (var appointment in affectedAppointments)

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
 develop

        var timeOff = new StylistTimeOff
 feature/s4-02/recalculate-discount-on-service-change-and-reports
 develop
 develop
        {
            appointment.StylistId = request.ReplacementStylistId.Value;
            appointment.UpdatedAt = DateTime.Now;
        }
    }

    var timeOff = new StylistTimeOff
    {
        StylistId = request.StylistId,
        OffDate = request.OffDate,
        IsFullDay = request.IsFullDay,

        StartTime = request.IsFullDay
            ? null
            : request.StartTime,

 feature/s4-02/recalculate-discount-on-service-change-and-reports
        EndTime = request.IsFullDay
            ? null
            : request.EndTime,

 feature/s2-02-stylist-time-off-warning
 feature/s2-02-stylist-time-off-warning

 feature/s2-02-stylist-time-off-booking
 develop
            Reason = request.Reason
 develop

        Reason = request.Reason.Trim()
    };

    dbContext.StylistTimeOffs.Add(timeOff);
    await dbContext.SaveChangesAsync();

 feature/s4-02/recalculate-discount-on-service-change-and-reports
    return Ok(new
    {
        timeOff,
        reassignedAppointmentIds = affectedAppointments.Select(appointment => appointment.AppointmentId)
    });
}

 feature/s2-02-stylist-time-off-warning
 feature/s2-02-stylist-time-off-warning

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
 develop

        return Ok(timeOff);
    }
 develop

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
 feature/s4-02/recalculate-discount-on-service-change-and-reports
 feature/s4-02/recalculate-discount-on-service-change-and-reports

 feature/s2-02-stylist-time-off-warning
 feature/s2-02-stylist-time-off-warning

 feature/s2-02-stylist-time-off-booking
 develop

 develop

    private async Task<string?> ValidateReplacementAsync(int replacementStylistId, int absentStylistId,
        DateOnly offDate, List<Appointment> appointments)
    {
        if (replacementStylistId == absentStylistId)
            return "Người thay thế phải là một thợ khác người nghỉ.";

        var replacement = await dbContext.Stylists.Include(stylist => stylist.Services)
            .FirstOrDefaultAsync(stylist => stylist.StylistId == replacementStylistId && stylist.IsActive);
        if (replacement is null) return "Không tìm thấy người thay thế đang hoạt động.";

        var day = offDate.ToDateTime(TimeOnly.MinValue);
        var nextDay = day.AddDays(1);
        var businessHours = await dbContext.BusinessHours.AsNoTracking()
            .SingleOrDefaultAsync(hours => hours.DayOfWeek == day.DayOfWeek);
        if (businessHours is null || businessHours.IsClosed || businessHours.OpensAt is null || businessHours.ClosesAt is null)
            return "Tiệm không hoạt động trong ngày này nên không thể chuyển lịch.";

        var shifts = await dbContext.WorkSchedules.AsNoTracking()
            .Where(schedule => schedule.StylistId == replacementStylistId && schedule.WorkDate >= day && schedule.WorkDate < nextDay && schedule.Status == "Working")
            .ToListAsync();
        var existingAppointments = await dbContext.Appointments.AsNoTracking()
            .Where(appointment => appointment.StylistId == replacementStylistId && appointment.AppointmentDate >= day && appointment.AppointmentDate < nextDay && appointment.Status != "Cancelled")
            .ToListAsync();
        var timeOffs = await dbContext.StylistTimeOffs.AsNoTracking()
            .Where(timeOff => timeOff.StylistId == replacementStylistId && timeOff.OffDate == offDate)
            .ToListAsync();
        var breaks = await dbContext.StylistBreaks.AsNoTracking()
            .Where(item => item.StylistId == replacementStylistId && item.BreakDate >= day && item.BreakDate < nextDay)
            .ToListAsync();
        var supportedServiceIds = replacement.Services.Select(link => link.ServiceId).ToHashSet();
        var open = businessHours.OpensAt.Value.ToTimeSpan();
        var close = businessHours.ClosesAt.Value.ToTimeSpan();

        foreach (var appointment in appointments)
        {
            if (appointment.AppointmentServices.Any(item => !supportedServiceIds.Contains(item.ServiceId)))
                return $"{replacement.FullName} không thực hiện đủ dịch vụ của lịch hẹn #{appointment.AppointmentId}.";
            if (appointment.StartTime < open || appointment.EndTime > close || !shifts.Any(shift => shift.StartTime <= appointment.StartTime && shift.EndTime >= appointment.EndTime))
                return $"{replacement.FullName} không có ca làm phù hợp cho lịch hẹn #{appointment.AppointmentId}.";
            if (existingAppointments.Any(item => appointment.StartTime < item.EndTime && appointment.EndTime > item.StartTime))
                return $"{replacement.FullName} đã có lịch trùng với lịch hẹn #{appointment.AppointmentId}.";
            if (breaks.Any(item => appointment.StartTime < item.EndTime && appointment.EndTime > item.StartTime) || timeOffs.Any(item => item.IsFullDay || (item.StartTime.HasValue && item.EndTime.HasValue && appointment.StartTime < item.EndTime.Value.ToTimeSpan() && appointment.EndTime > item.StartTime.Value.ToTimeSpan())))
                return $"{replacement.FullName} có thời gian nghỉ trùng với lịch hẹn #{appointment.AppointmentId}.";
        }

        return null;
    }

 develop
}

public class StylistTimeOffRequest
{
    public int StylistId { get; set; }

    public DateOnly OffDate { get; set; }

    public bool IsFullDay { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Reason { get; set; }
 feature/s4-02/recalculate-discount-on-service-change-and-reports
 feature/s4-02/recalculate-discount-on-service-change-and-reports

 feature/s2-02-stylist-time-off-warning
 feature/s2-02-stylist-time-off-warning

 feature/s2-02-stylist-time-off-booking
 develop
}

 develop

    public int? ReplacementStylistId { get; set; }
}

}
 develop
