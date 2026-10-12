using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed record UpdateCustomerProfileRequest(
    [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
    [param: EmailAddress, StringLength(256)] string? Email,
    [param: StringLength(2000)] string? Notes);

public sealed record CustomerProfileResponse(
    int CustomerId,
    string FullName,
    string Phone,
    string? Email,
    int VisitCount,
    int NoShowCount,
    string? Notes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
