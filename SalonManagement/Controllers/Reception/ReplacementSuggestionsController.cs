using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/replacement-suggestions")]
public sealed class ReplacementSuggestionsController(AppointmentReplacementSuggestionService suggestions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AppointmentReplacementSuggestion>> Get([FromQuery] int appointmentId, [FromQuery] int absentStylistId)
    {
        if (appointmentId <= 0 || absentStylistId <= 0)
            return BadRequest(new { error = "Lịch hẹn và thợ nghỉ không hợp lệ." });
        return Ok(await suggestions.SuggestAsync(appointmentId, absentStylistId));
    }
}
