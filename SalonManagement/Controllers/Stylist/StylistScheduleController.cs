using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.StylistPortal;

[Authorize(Roles = UserRoles.Stylist)]
[Route("stylist/schedule")]
public sealed class StylistScheduleController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? date)
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        var stylist = await db.Stylists.AsNoTracking().SingleOrDefaultAsync(item => item.Email == email && item.IsActive);
        if (stylist is null) return Forbid();
        var selected = (date ?? DateTime.Today).Date;
        var appointments = await db.Appointments.AsNoTracking().Include(item => item.Customer).Include(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => item.StylistId == stylist.StylistId && item.AppointmentDate == selected)
            .OrderBy(item => item.StartTime).ToListAsync();
        return View((stylist.FullName, selected, appointments));
    }

    [HttpPost("{appointmentId:int}/start")]
    public Task<IActionResult> Start(int appointmentId) => ChangeStatus(appointmentId, "Arrived", "InProgress", true);
    [HttpPost("{appointmentId:int}/complete")]
    public Task<IActionResult> Complete(int appointmentId) => ChangeStatus(appointmentId, "InProgress", "Completed", false);
    private async Task<IActionResult> ChangeStatus(int appointmentId, string expected, string next, bool starting)
    {
        var email=User.FindFirstValue(ClaimTypes.Email)??User.Identity?.Name; var stylist=await db.Stylists.SingleOrDefaultAsync(x=>x.Email==email&&x.IsActive);
        var appointment=stylist is null?null:await db.Appointments.SingleOrDefaultAsync(x=>x.AppointmentId==appointmentId&&x.StylistId==stylist.StylistId);
        if(appointment is null) return NotFound(); if(appointment.Status!=expected) return BadRequest(new{error="Trạng thái lịch hẹn không hợp lệ."});
        appointment.Status=next; if(starting) appointment.ActualStartedAt=DateTime.Now; else appointment.ActualCompletedAt=DateTime.Now; appointment.UpdatedAt=DateTime.Now; await db.SaveChangesAsync(); return Ok(new{appointment.Status});
    }
}
