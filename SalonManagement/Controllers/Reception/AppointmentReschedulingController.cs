using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/appointments")]
public sealed class AppointmentReschedulingController(AppointmentReschedulingService service, ApplicationDbContext db) : ControllerBase
{
    [HttpPost("{appointmentId:int}/reschedule-preview")]
    public async Task<IActionResult> Preview(int appointmentId, [FromBody] RescheduleAppointmentRequest request)
    {
        var result = await service.PreviewAsync(appointmentId, request);
        return result.Succeeded ? Ok(new { valid = true }) : BadRequest(new { valid = false, code = result.ErrorCode, error = result.Error });
    }

    [HttpPut("{appointmentId:int}/reschedule")]
    public async Task<IActionResult> Reschedule(int appointmentId, [FromBody] RescheduleAppointmentRequest request)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorId)) return Unauthorized();
        var actorName = User.FindFirst("email")?.Value ?? User.Identity?.Name ?? actorId;
        var result = await service.RescheduleAsync(appointmentId, request, new AppointmentChangeActor(actorId, actorName));
        if (!result.Succeeded)
        {
            var response = new { code = result.ErrorCode, error = result.Error };
            return result.ErrorCode == "not_found" ? NotFound(response) : BadRequest(response);
        }

        var appointment = result.Appointment!;
        return Ok(new { appointment.AppointmentId, appointment.StylistId, appointment.StartTime, appointment.EndTime, appointment.UpdatedAt });
    }

    [HttpGet("{appointmentId:int}/change-history")]
    public async Task<IActionResult> ChangeHistory(int appointmentId)
    {
        var items = await db.AppointmentChangeLogs.AsNoTracking()
            .Where(log => log.AppointmentId == appointmentId)
            .OrderByDescending(log => log.ChangedAt)
            .Select(log => new { log.ActorName, log.ChangedAt, log.OldStylistId, log.NewStylistId, log.OldStartTime, log.NewStartTime, log.EmailStatus })
            .ToListAsync();
        return Ok(items);
    }
}
