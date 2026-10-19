using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.StylistPortal;

[Authorize(Roles = UserRoles.Stylist)]
[ApiController]
[Route("api/stylist/leave-requests")]
public sealed class StylistLeaveRequestsController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStylistLeaveRequest request)
    {
        var email = User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? User.FindFirstValue(ClaimTypes.Email);
        var stylist = string.IsNullOrWhiteSpace(email) ? null : await db.Stylists.SingleOrDefaultAsync(item => item.Email == email && item.IsActive);
        if (stylist is null) return Forbid();
        var reason = request.Reason?.Trim();
        if (request.OffDate == default || string.IsNullOrWhiteSpace(reason)) return BadRequest(new { error = "Vui lòng chọn ngày nghỉ và nêu lý do." });
        if (!request.IsFullDay && (!request.StartTime.HasValue || !request.EndTime.HasValue || request.StartTime >= request.EndTime)) return BadRequest(new { error = "Khoảng thời gian xin nghỉ không hợp lệ." });
        var leave = new StylistLeaveRequest { StylistId = stylist.StylistId, OffDate = request.OffDate, IsFullDay = request.IsFullDay, StartTime = request.IsFullDay ? null : request.StartTime, EndTime = request.IsFullDay ? null : request.EndTime, Reason = reason, Status = "Pending" };
        db.StylistLeaveRequests.Add(leave);
        var receptionists = await users.GetUsersInRoleAsync(UserRoles.Receptionist);
        foreach (var receptionist in receptionists.Where(user => !string.IsNullOrWhiteSpace(user.Email)))
            db.StaffNotifications.Add(new StaffNotification { RecipientEmail = receptionist.Email!, AppointmentId = 0, Type = "StylistLeaveRequest", Message = $"Thợ {stylist.FullName} xin nghỉ ngày {request.OffDate:dd/MM/yyyy}: {reason}" });
        await db.SaveChangesAsync();
        return Ok(new { message = "Đã gửi yêu cầu xin nghỉ cho lễ tân để điều phối lịch.", leave.StylistLeaveRequestId });
    }
}

public sealed record CreateStylistLeaveRequest(DateOnly OffDate, bool IsFullDay, TimeOnly? StartTime, TimeOnly? EndTime, string? Reason);
