using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record RegisterStaffRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string Role);

public sealed record ConfirmEmailRequest(
    [Required, EmailAddress] string Email,
    [Required, RegularExpression(@"^\d{6}$")] string Code);

public sealed record ResendEmailVerificationRequest(
    [Required, EmailAddress] string Email);

public sealed record CreateStaffAccountRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(120)] string FullName,
    [Required] string Role,
    [Required, RegularExpression(@"^0\d{9}$")] string PhoneNumber,
    bool IsActive = true);

public sealed record RefreshRequest([Required] string RefreshToken);

public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string Role,
    string RedirectUrl);
