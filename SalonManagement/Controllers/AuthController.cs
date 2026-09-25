using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    ITokenService tokenService,
    TimeProvider timeProvider) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Email hoặc mật khẩu không chính xác.";

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        var passwordResult = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordResult)
        {
            if (user.IsActive)
            {
                await userManager.AccessFailedAsync(user);
            }
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Tài khoản đã ngưng hoạt động. Vui lòng liên hệ quản trị viên." });
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return Ok(await IssueTokens(user));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh(RefreshRequest request)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == hash);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (storedToken is null || !storedToken.IsValid(now) || !storedToken.User.IsActive)
        {
            return Unauthorized(new { message = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." });
        }

        storedToken.RevokedAtUtc = now;
        return Ok(await IssueTokens(storedToken.User));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash);
        if (storedToken is not null && storedToken.RevokedAtUtc is null)
        {
            storedToken.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await dbContext.SaveChangesAsync();
        }
        return NoContent();
    }

    private async Task<TokenResponse> IssueTokens(ApplicationUser user)
    {
        var (accessToken, accessExpiry) = tokenService.CreateAccessToken(user);
        var refreshToken = tokenService.CreateRefreshToken();
        var refreshExpiry = tokenService.GetRefreshTokenExpiry();
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = tokenService.HashRefreshToken(refreshToken),
            UserId = user.Id,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            ExpiresAtUtc = refreshExpiry
        });
        await dbContext.SaveChangesAsync();
        return new TokenResponse(accessToken, accessExpiry, refreshToken, refreshExpiry);
    }
}
