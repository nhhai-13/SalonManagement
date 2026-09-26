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
    private const string LockedAccountMessage = "Tài khoản đã bị khóa tạm thời do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau 15 phút.";

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterStaffRequest request)
    {
        var role = NormalizeStaffRole(request.Role);
        if (role is null)
        {
            return BadRequest(new { message = "Chỉ lễ tân và thợ được phép tự đăng ký tài khoản." });
        }

        if (await userManager.FindByEmailAsync(request.Email.Trim()) is not null)
        {
            return Conflict(new { message = "Email này đã được sử dụng." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = FormatIdentityErrors(result) });
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return BadRequest(new { message = "Không thể gán vai trò cho tài khoản. Vui lòng thử lại." });
        }

        return Created(string.Empty, new { message = "Đăng ký thành công. Bạn có thể đăng nhập ngay.", role });
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { message = LockedAccountMessage });
        }

        var passwordResult = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordResult)
        {
            if (user.IsActive)
            {
                await userManager.AccessFailedAsync(user);
                if (await userManager.IsLockedOutAsync(user))
                {
                    return Unauthorized(new { message = LockedAccountMessage });
                }
            }
            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { message = LockedAccountMessage });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Tài khoản đã ngưng hoạt động. Vui lòng liên hệ quản trị viên." });
        }

        var requiredRoles = request.Portal?.ToLowerInvariant() switch
        {
            "admin" => new[] { "Admin", "Owner" },
            "reception" => new[] { "Receptionist" },
            "stylist" => new[] { "Stylist" },
            _ => Array.Empty<string>()
        };
        if (requiredRoles.Length > 0 && !await HasAnyRole(user, requiredRoles))
        {
            return Unauthorized(new { message = "Tài khoản không có quyền truy cập khu vực này." });
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
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpiry) = tokenService.CreateAccessToken(user, roles);
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

    private async Task<bool> HasAnyRole(ApplicationUser user, IEnumerable<string> roles)
    {
        foreach (var role in roles)
        {
            if (await userManager.IsInRoleAsync(user, role)) return true;
        }
        return false;
    }

    public static string? NormalizeStaffRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "reception" or "receptionist" => "Receptionist",
        "stylist" => "Stylist",
        _ => null
    };

    internal static string FormatIdentityErrors(IdentityResult result) => string.Join(" ",
        result.Errors.Select(error => error.Code switch
        {
            "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
            "PasswordRequiresDigit" => "Mật khẩu phải có ít nhất một chữ số.",
            "PasswordRequiresLower" => "Mật khẩu phải có ít nhất một chữ thường.",
            "DuplicateUserName" or "DuplicateEmail" => "Email này đã được sử dụng.",
            _ => error.Description
        }));
}
