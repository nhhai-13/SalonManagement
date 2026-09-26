using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    string? Portal = null);

public sealed record RegisterStaffRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string Role);

public sealed record CreateStaffAccountRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string Role);

public sealed record RefreshRequest([Required] string RefreshToken);

public sealed record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
