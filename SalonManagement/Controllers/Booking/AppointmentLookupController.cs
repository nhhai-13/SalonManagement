using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Data;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[AllowAnonymous]
[Route("appointments/lookup")]
public sealed class AppointmentLookupController(ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("search")]
    public async Task<IActionResult> Search(string? query)
    {
        var result = await new AppointmentLookupService(db).SearchAsync(query);
        return Ok(new { error = result.Error, appointments = result.Items.Select(item => new { item.Reference, date = item.Date.ToString("dd/MM/yyyy"), startTime = item.StartTime.ToString(@"hh\:mm"), endTime = item.EndTime.ToString(@"hh\:mm"), item.Services, item.StylistName, item.DurationMinutes, item.Status }) });
    }
}
