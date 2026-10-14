using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
namespace SalonManagement.Controllers;
[Authorize(Roles = RoleGroups.FrontDesk)]
public sealed class ScheduleCoordinationPagesController : Controller
{
    [HttpGet("reception/schedule-coordination")]
    public IActionResult Index() => View("~/Views/Admin/ScheduleCoordination.cshtml");
    [HttpGet("reception/schedule-coordination/follow-ups")]
    public IActionResult FollowUps() => View("~/Views/Admin/ScheduleFollowUps.cshtml");
}