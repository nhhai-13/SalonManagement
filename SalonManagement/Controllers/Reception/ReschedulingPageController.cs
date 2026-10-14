using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
namespace SalonManagement.Controllers;
[Authorize(Roles = RoleGroups.FrontDesk)]
public sealed class ReschedulingPageController : Controller
{
    [HttpGet("reception/rescheduling")]
    public IActionResult Index() => View("~/Views/Reception/Rescheduling.cshtml");
}