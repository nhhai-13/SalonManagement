using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed record ResolveBookingCustomerRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string FullName,
    [property: Required, StringLength(30, MinimumLength = 7)] string Phone,
    [property: EmailAddress, StringLength(256)] string? Email);

public sealed record ResolveBookingCustomerResponse(
    bool IsReturningCustomer,
    bool Created);
