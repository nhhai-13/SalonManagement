using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalonManagement.Controllers;

public class AdminController : Controller
{
    [HttpGet("admin/login")]
    public IActionResult Login()
    {
        ViewData["Portal"] = "admin";
        return View();
    }

    [HttpGet("reception/login")]
    public IActionResult ReceptionLogin()
    {
        ViewData["Portal"] = "reception";
        return View("Login");
    }

    [HttpGet("stylist/login")]
    public IActionResult StylistLogin()
    {
        ViewData["Portal"] = "stylist";
        return View("Login");
    }

    [HttpGet("admin")]
    public IActionResult Index() => View();

    [HttpGet("admin/business-hours")]
    public IActionResult BusinessHours() => View();
}

[Authorize]
[ApiController]
[Route("api/admin")]
public class AdminApiController : ControllerBase
{
    [HttpGet("session")]
    public IActionResult Session() => Ok(new { authenticated = true, email = User.FindFirst("email")?.Value });
}
