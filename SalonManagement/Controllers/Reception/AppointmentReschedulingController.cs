using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/appointments")]
public sealed class AppointmentReschedulingController(AppointmentReschedulingService service) : ControllerBase
{
    [HttpPut("{appointmentId:int}/reschedule")]
    public async Task<IActionResult> Reschedule(int appointmentId, [FromBody] RescheduleAppointmentRequest request)
    {
        var result = await service.RescheduleAsync(appointmentId, request);
        if (!result.Succeeded)
        {
            var response = new { code = result.ErrorCode, error = result.Error };
            return result.ErrorCode == "not_found" ? NotFound(response) : BadRequest(response);
        }

        var appointment = result.Appointment!;
        return Ok(new { appointment.AppointmentId, appointment.StylistId, appointment.StartTime, appointment.EndTime, appointment.UpdatedAt });
    }
}
