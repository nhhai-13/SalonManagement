using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[ApiController]
[Route("api/reception/bookings")]
[Authorize(Roles = RoleGroups.FrontDesk)]
public sealed class ReceptionBookingsController(BookingService bookings) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateBookingResponse>> Create(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await bookings.CreateAsync(
                request,
                confirmedByReception: true,
                cancellationToken: cancellationToken,
                changedByUserId: User.FindFirst("sub")?.Value ?? string.Empty,
                changedByEmail: User.FindFirst("email")?.Value ?? string.Empty));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (DuplicateCustomerPhoneException exception)
        {
            return Conflict(new { message = exception.Message, code = "duplicate_customer_phone" });
        }
        catch (BookingConflictException exception)
        {
            return Conflict(new { message = exception.Message, code = "booking_conflict" });
        }
    }
}
