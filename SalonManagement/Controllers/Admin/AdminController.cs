using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;
using System.Security.Cryptography;
using System.Security.Claims;

namespace SalonManagement.Controllers;

public class AdminController : Controller
{
    private const string LoginView = "~/Views/Admin/Authentication/Login.cshtml";
    private const string RegisterView = "~/Views/Admin/Authentication/Register.cshtml";
    private const string VerifyEmailView = "~/Views/Admin/Authentication/VerifyEmail.cshtml";

    [HttpGet("admin/login")]
    public IActionResult Login()
    {
        ViewData["Portal"] = "admin";
        return View(LoginView);
    }

    [HttpGet("reception/login")]
    public IActionResult ReceptionLogin() => Redirect($"/admin/login{Request.QueryString}");

    [HttpGet("owner/login")]
    public IActionResult OwnerLogin() => Redirect($"/admin/login{Request.QueryString}");

    [HttpGet("stylist/login")]
    public IActionResult StylistLogin() => Redirect($"/admin/login{Request.QueryString}");

    [HttpGet("reception/register")]
    public IActionResult ReceptionRegister()
    {
        ViewData["Portal"] = "reception";
        return View(RegisterView);
    }

    [HttpGet("stylist/register")]
    public IActionResult StylistRegister()
    {
        ViewData["Portal"] = "stylist";
        return View(RegisterView);
    }

    [HttpGet("verify-email")]
    public IActionResult VerifyEmail([FromQuery] string? email, [FromQuery] string? portal)
    {
        ViewData["Email"] = email?.Trim() ?? string.Empty;
        ViewData["Portal"] = portal?.Trim().ToLowerInvariant() == "stylist" ? "stylist" : "reception";
        return View(VerifyEmailView);
    }

    [HttpGet("admin")]
    public IActionResult Index() => View();

    [HttpGet("owner")]
    public IActionResult Owner() => View();

    [HttpGet("owner/business-hours")]
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

[ApiController]
[Route("api/admin")]
public class AdminApiController(UserManager<ApplicationUser> userManager, IEmailService emailService,
    ILogger<AdminApiController> logger) : ControllerBase
{
    [Authorize(Roles = UserRoles.Admin)]
    [HttpGet("session")]
    public IActionResult Session() => Ok(new
    {
        authenticated = true,
        email = User.FindFirst("email")?.Value,
        role = UserRoles.Admin
    });

    [Authorize(Roles = UserRoles.Admin)]
    [HttpPost("staff-accounts")]
    public async Task<IActionResult> CreateStaffAccount(CreateStaffAccountRequest request)
    {
        var role = request.Role?.Trim() switch
        {
            UserRoles.Admin => UserRoles.Admin,
            UserRoles.Owner => UserRoles.Owner,
            UserRoles.Receptionist => UserRoles.Receptionist,
            UserRoles.Stylist => UserRoles.Stylist,
            _ => null
        };
        if (role is null)
        {
            return BadRequest(new { message = "Vai trò nội bộ không hợp lệ." });
        }

        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            return Conflict(new { message = "Email này đã được sử dụng." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            IsActive = request.IsActive,
            MustChangePassword = true,
            CreatedByUserId = User.FindFirstValue("sub")
        };
        var temporaryPassword = $"Aa1!{Convert.ToHexString(RandomNumberGenerator.GetBytes(18))}";
        var result = await userManager.CreateAsync(user, temporaryPassword);
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

        try
        {
            await emailService.SendTemporaryPasswordEmailAsync(user.Email!, temporaryPassword);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not deliver staff invitation to {UserId}.", user.Id);
            user.IsActive = false;
            await userManager.UpdateAsync(user);
            var cleanup = await userManager.DeleteAsync(user);
            return StatusCode(503, new { message = cleanup.Succeeded
                ? "Không gửi được email mật khẩu tạm. Tài khoản chưa được cấp; vui lòng kiểm tra SMTP rồi thử lại."
                : "Không gửi được email mật khẩu tạm. Tài khoản đã ngừng hoạt động; vui lòng liên hệ quản trị viên." });
        }
        return Created(string.Empty, new { message = "Đã tạo tài khoản và gửi mật khẩu tạm qua email. Người dùng phải đổi mật khẩu khi đăng nhập lần đầu.", user.Email, role });
    }
}


[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/owner")]
public class OwnerApiController : ControllerBase
{
    [HttpGet("session")]
    public IActionResult Session() => Ok(new
    {
        authenticated = true,
        email = User.FindFirst("email")?.Value,
        role = UserRoles.Owner
    });
}
