using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Admin)]
[ApiController]
[Route("api/admin/staff")]
public sealed class StaffController(IStaffAccountService staffService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StaffAccountListResponse>> GetList(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? isActive) => Ok(await staffService.GetAccountsAsync(search, role, isActive));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateStaffAccountRequest request)
    {
        var result = await staffService.UpdateAsync(id, request);
        return result.Success ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ChangeStatus(string id, ChangeStaffStatusRequest request)
    {
        var result = await staffService.ChangeStatusAsync(id, request.IsActive);
        return result.Success ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });
    }

    [HttpPost("{id}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(string id)
    {
        var result = await staffService.RevokeSessionsAsync(id);
        return result.Success ? Ok(new { message = result.Message }) : BadRequest(new { message = result.Message });
    }
}
