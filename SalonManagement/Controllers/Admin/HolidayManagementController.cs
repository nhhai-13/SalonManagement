using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
namespace SalonManagement.Controllers;
[Authorize(Roles = UserRoles.Owner)]
public class HolidayManagementController : Controller
{
    [HttpGet("owner/holidays")]
    public IActionResult Index() => View();
}
