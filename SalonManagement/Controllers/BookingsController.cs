using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[ApiController]
[Route("api/bookings")]
[AllowAnonymous]
public sealed class BookingsController(BookingService bookings) : ControllerBase
{
    [HttpGet("options")]
    public Task<BookingOptionsResponse> Options(CancellationToken cancellationToken) =>
        bookings.GetOptionsAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CreateBookingResponse>> Create(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await bookings.CreateAsync(request, confirmedByReception: false, cancellationToken));
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
