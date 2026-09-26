using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class TokenServiceTests
{
    [Fact]
    public void Tokens_UseConfiguredThirtyMinutesAndSevenDays()
    {
        var now = new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Jwt:Key"] = "a-test-key-that-is-longer-than-thirty-two-characters",
            ["Jwt:AccessTokenMinutes"] = "30",
            ["Jwt:RefreshTokenDays"] = "7"
        }).Build();
        var service = new TokenService(configuration, new FixedTimeProvider(now));

        var access = service.CreateAccessToken(
            new ApplicationUser { Id = "1", Email = "admin@example.com" },
            ["Admin"]);

        Assert.Equal(now.UtcDateTime.AddMinutes(30), access.ExpiresAtUtc);
        Assert.Equal(now.UtcDateTime.AddDays(7), service.GetRefreshTokenExpiry());
        Assert.NotEqual(service.CreateRefreshToken(), service.CreateRefreshToken());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access.Token);
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
