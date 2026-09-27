using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;

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

    [HttpGet("reception/register")]
    public IActionResult ReceptionRegister()
    {
        ViewData["Portal"] = "reception";
        return View("Register");
    }

    [HttpGet("stylist/register")]
    public IActionResult StylistRegister()
    {
        ViewData["Portal"] = "stylist";
        return View("Register");
    }

    [HttpGet("admin")]
    public IActionResult Index() => View();

    [HttpGet("admin/business-hours")]
    public IActionResult BusinessHours() => View();
}

public class StaffPortalController : Controller
{
    [HttpGet("reception")]
    public IActionResult Reception() => View();

    [HttpGet("stylist")]
    public IActionResult Stylist() => View();
}

[ApiController]
[Route("api/portal")]
public class StaffPortalApiController : ControllerBase
{
    [Authorize(Roles = UserRoles.Receptionist)]
    [HttpGet("reception/session")]
    public IActionResult ReceptionSession() => Session(UserRoles.Receptionist);

    [Authorize(Roles = UserRoles.Stylist)]
    [HttpGet("stylist/session")]
    public IActionResult StylistSession() => Session(UserRoles.Stylist);

    private IActionResult Session(string role) => Ok(new
    {
        authenticated = true,
        email = User.FindFirst("email")?.Value,
        role
    });
}

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin")]
public class AdminApiController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet("session")]
    public IActionResult Session() => Ok(new { authenticated = true, email = User.FindFirst("email")?.Value });

    [HttpPost("staff-accounts")]
    public async Task<IActionResult> CreateStaffAccount(CreateStaffAccountRequest request)
    {
        var role = AuthController.NormalizeStaffRole(request.Role);
        if (role is null)
        {
            return BadRequest(new { message = "Vai trò chỉ có thể là lễ tân hoặc thợ." });
        }

        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            return Conflict(new { message = "Email này đã được sử dụng." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = AuthController.FormatIdentityErrors(result) });
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return BadRequest(new { message = "Không thể gán vai trò cho tài khoản." });
        }

        return Created(string.Empty, new { message = "Đã tạo tài khoản nhân viên.", user.Email, role });
    }
}
