using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

public class AdminController : Controller
{
    // Trang đăng nhập phải cho phép người chưa đăng nhập truy cập
    [AllowAnonymous]
    [HttpGet("admin/login")]
    public IActionResult Login() => View();

    // Chỉ Admin được truy cập trang quản trị
    [Authorize(Roles = UserRoles.Admin)]
    [HttpGet("admin")]
    public IActionResult Index() => View();

    // Chỉ Admin được truy cập quản lý giờ làm việc
    [Authorize(Roles = UserRoles.Admin)]
    [HttpGet("admin/business-hours")]
    public IActionResult BusinessHours() => View();
}

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin")]
public class AdminApiController : ControllerBase
{
    [HttpGet("session")]
    public IActionResult Session()
    {
        return Ok(new
        {
            authenticated = true,
            email = User.FindFirst("email")?.Value
        });
    }
}