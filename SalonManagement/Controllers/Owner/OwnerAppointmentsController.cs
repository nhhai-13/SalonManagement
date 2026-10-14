using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
public sealed class OwnerAppointmentsController(ApplicationDbContext db) : Controller
{
    [HttpGet("owner/appointments")]
    public async Task<IActionResult> Index()
    {
        var appointments = await db.Appointments.AsNoTracking()
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.Stylist)
            .Include(appointment => appointment.AppointmentServices).ThenInclude(item => item.Service)
            .Where(appointment => appointment.Status != "Cancelled")
            .OrderByDescending(appointment => appointment.CreatedAt)
            .Take(100)
            .ToListAsync();
        return View(appointments);
    }
}

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/owner/appointments")]
public sealed class OwnerAppointmentsApiController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var since = DateTime.Now.AddDays(-1);
        var count = await db.Appointments.AsNoTracking()
            .CountAsync(appointment => appointment.Status != "Cancelled" && appointment.CreatedAt >= since);
        return Ok(new { count });
    }
}
