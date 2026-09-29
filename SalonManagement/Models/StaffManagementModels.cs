using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed record StaffAccountResponse(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    bool IsLockedOut,
    bool MustChangePassword,
    DateTime CreatedAtUtc,
    string? CreatedByUserId);

public sealed record ChangeStaffStatusRequest(bool IsActive);

public sealed record CreateStaffAccountRequest(
    [Required, StringLength(120)] string FullName,
    [Required, EmailAddress] string Email,
    [Phone] string? PhoneNumber,
    [Required] string Role,
    bool IsActive = true);

public sealed record UpdateStaffAccountRequest(
    [Required, StringLength(120)] string FullName,
    [Required, EmailAddress] string Email,
    [Phone] string? PhoneNumber,
    [Required] string Role);

public sealed record StaffAccountListResponse(
    IReadOnlyList<StaffAccountResponse> Items,
    int TotalCount);
