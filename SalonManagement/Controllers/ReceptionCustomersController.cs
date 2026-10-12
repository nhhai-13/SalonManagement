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
    [HttpGet]
    public async Task<ActionResult<CustomerSearchResponse>> Search(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        try { return Ok(await customers.SearchAsync(query, cancellationToken)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<CustomerPhoneLookupResponse>> LookupByPhone(
        [FromQuery] string phone,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await customers.LookupByPhoneAsync(phone, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (DuplicateCustomerPhoneException exception)
        {
            return Conflict(new { message = exception.Message, code = "duplicate_customer_phone" });
        }
    }

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
