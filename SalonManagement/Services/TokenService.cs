using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SalonManagement.Models;

namespace SalonManagement.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
    DateTime GetRefreshTokenExpiry();
}

public sealed class TokenService(IConfiguration configuration, TimeProvider timeProvider) : ITokenService
{
    private readonly IConfigurationSection _jwt = configuration.GetSection("Jwt");

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(ApplicationUser user)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_jwt.GetValue<int>("AccessTokenMinutes", 30));
        var key = _jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_jwt["Issuer"], _jwt["Audience"], claims, now, expires, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public DateTime GetRefreshTokenExpiry() => timeProvider.GetUtcNow().UtcDateTime.AddDays(_jwt.GetValue<int>("RefreshTokenDays", 7));
}
