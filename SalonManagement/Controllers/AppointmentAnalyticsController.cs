using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = UserRoles.Owner)]
public class AppointmentAnalyticsController(AppointmentAnalyticsService service, TimeProvider clock) : Controller
{
    [HttpGet("admin/appointment-analytics")]
    public IActionResult Index()
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        return View(new AppointmentAnalyticsRequest
        {
            StartDate = new DateOnly(today.Year, today.Month, 1), EndDate = today
        });
    }

    [HttpGet("api/appointment-analytics")]
    public async Task<IActionResult> Get([FromQuery] AppointmentAnalyticsRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ValidationProblemDetails(ModelState) { Status = 400 });
        return Ok(await service.GetAsync(request, cancellationToken));
    }
}
