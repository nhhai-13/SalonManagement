using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[ApiController]
[Route("api/reception/customers")]
[Authorize(Roles = RoleGroups.FrontDesk)]
public sealed class ReceptionCustomersController(CustomerProfileManagementService customers) : ControllerBase
{
    [HttpGet("{customerId:int}")]
    public async Task<ActionResult<CustomerProfileResponse>> Get(
        int customerId,
        CancellationToken cancellationToken)
    {
        if (customerId <= 0)
            return BadRequest(new { message = "Mã hồ sơ không hợp lệ." });

        var profile = await customers.GetByIdAsync(customerId, cancellationToken);
        return profile is null
            ? NotFound(new { message = "Không tìm thấy hồ sơ khách." })
            : Ok(profile);
    }

    [HttpPut("{customerId:int}")]
    public async Task<ActionResult<CustomerProfileResponse>> Update(
        int customerId,
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (customerId <= 0)
            return BadRequest(new { message = "Mã hồ sơ không hợp lệ." });

        try
        {
            var profile = await customers.UpdateAsync(
                customerId,
                request,
                User.FindFirst("sub")?.Value ?? string.Empty,
                User.FindFirst("email")?.Value ?? string.Empty,
                cancellationToken);
            return profile is null
                ? NotFound(new { message = "Không tìm thấy hồ sơ khách." })
                : Ok(profile);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
