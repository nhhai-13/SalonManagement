using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed record StaffAccountResponse(
    string Id,
    string Email,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    bool IsLockedOut);

public sealed record ChangeStaffStatusRequest(bool IsActive);

public sealed record UpdateStaffAccountRequest(
    [Required, EmailAddress] string Email,
    string? PhoneNumber,
    [Required] string Role);

public sealed record StaffAccountListResponse(
    IReadOnlyList<StaffAccountResponse> Items,
    int TotalCount);
