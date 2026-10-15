using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using System.Security.Cryptography;

namespace SalonManagement.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    ITokenService tokenService,
    TimeProvider timeProvider,
    IEmailService emailService,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Email hoặc mật khẩu không chính xác.";
    private const string LockedAccountMessage = "Tài khoản đã bị khóa tạm thời do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau 15 phút hoặc liên hệ quản trị viên để được mở khóa.";

    [Authorize]
    [HttpGet("session")]
    public IActionResult Session()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
        return Ok(new { role, redirectUrl = GetRedirectUrl(role) });
    }


    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterStaffRequest request)
    {
        var role = NormalizeStaffRole(request.Role);
        if (role is null)
        {
            return BadRequest(new { message = "Chỉ lễ tân và thợ được phép tự đăng ký tài khoản." });
        }

        var email = request.Email.Trim();
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser?.EmailConfirmed == true)
        {
            return Conflict(new { message = "Email này đã được sử dụng." });
        }

        if (existingUser is not null)
        {
            var deleteResult = await userManager.DeleteAsync(existingUser);
            if (!deleteResult.Succeeded)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "Không thể làm mới yêu cầu đăng ký. Vui lòng thử lại." });
            }
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
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

        try
        {
            await CreateAndSendEmailVerificationCodeAsync(user);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không thể gửi mã xác minh khi đăng ký cho {Email}", email);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Tài khoản đã được tạo nhưng chưa thể gửi mã xác minh. Vui lòng thử gửi lại sau.",
                requiresEmailVerification = true,
                email
            });
        }

        return Accepted(new
        {
            message = "Mã xác minh đã được gửi đến email của bạn.",
            requiresEmailVerification = true,
            email,
            role,
            redirectUrl = $"/verify-email?email={Uri.EscapeDataString(email)}&portal={Uri.EscapeDataString(request.Role)}"
        });
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (user is null || user.EmailConfirmed || string.IsNullOrWhiteSpace(user.EmailVerificationCodeHash))
        {
            return BadRequest(new { message = "Mã xác minh không hợp lệ hoặc đã được sử dụng." });
        }

        if (user.EmailVerificationCodeExpiresAtUtc is null || user.EmailVerificationCodeExpiresAtUtc <= now)
        {
            return BadRequest(new { message = "Mã xác minh đã hết hạn. Vui lòng yêu cầu gửi lại mã mới." });
        }

        if (user.EmailVerificationFailedAttempts >= 5)
        {
            return BadRequest(new { message = "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu gửi lại mã mới." });
        }

        var verificationResult = userManager.PasswordHasher.VerifyHashedPassword(
            user,
            user.EmailVerificationCodeHash,
            request.Code);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            user.EmailVerificationFailedAttempts++;
            await userManager.UpdateAsync(user);
            var remainingAttempts = Math.Max(0, 5 - user.EmailVerificationFailedAttempts);
            return BadRequest(new
            {
                message = remainingAttempts > 0
                    ? $"Mã xác minh không chính xác. Bạn còn {remainingAttempts} lần thử."
                    : "Bạn đã nhập sai quá nhiều lần. Vui lòng yêu cầu gửi lại mã mới.",
                remainingAttempts
            });
        }

        user.EmailConfirmed = true;
        user.EmailVerificationCodeHash = null;
        user.EmailVerificationCodeExpiresAtUtc = null;
        user.EmailVerificationCodeSentAtUtc = null;
        user.EmailVerificationFailedAttempts = 0;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Không thể xác nhận email. Vui lòng thử lại." });
        }

        return Ok(new { message = "Xác minh email thành công. Bạn có thể đăng nhập ngay." });
    }

    [HttpPost("resend-email-verification")]
    public async Task<IActionResult> ResendEmailVerification(ResendEmailVerificationRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.EmailConfirmed)
        {
            return Ok(new { message = "Nếu email đang chờ xác minh, một mã mới sẽ được gửi." });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (user.EmailVerificationCodeSentAtUtc is not null &&
            user.EmailVerificationCodeSentAtUtc.Value.AddMinutes(1) > now)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "Vui lòng chờ 60 giây trước khi yêu cầu mã mới." });
        }

        try
        {
            await CreateAndSendEmailVerificationCodeAsync(user);
            return Ok(new { message = "Mã xác minh mới đã được gửi đến email của bạn." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không thể gửi lại mã xác minh cho {Email}", request.Email);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Chưa thể gửi mã xác minh. Vui lòng thử lại sau." });
        }
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
            return await CreateLockedAccountResponseAsync(user);
        }

        var passwordResult = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordResult)
        {
            if (user.IsActive)
            {
                await userManager.AccessFailedAsync(user);
                if (await userManager.IsLockedOutAsync(user))
                {
                    return await CreateLockedAccountResponseAsync(user);
                }

                var failedAttempts = await userManager.GetAccessFailedCountAsync(user);
                var remainingAttempts = Math.Max(
                    0,
                    userManager.Options.Lockout.MaxFailedAccessAttempts - failedAttempts);

                return Unauthorized(new
                {
                    message = GetInvalidCredentialsMessage(remainingAttempts),
                    remainingAttempts
                });
            }

            return Unauthorized(new { message = InvalidCredentialsMessage });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return await CreateLockedAccountResponseAsync(user);
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { message = "Tài khoản đã ngưng hoạt động. Vui lòng liên hệ quản trị viên." });
        }

        if (!user.EmailConfirmed)
        {
            return Unauthorized(new
            {
                message = "Email chưa được xác minh. Vui lòng nhập mã đã gửi đến email của bạn.",
                requiresEmailVerification = true,
                email = user.Email
            });
        }

        var role = await GetSingleActorRole(user);
        if (role is null)
        {
            return Unauthorized(new { message = "Tài khoản phải được gán đúng một vai trò để đăng nhập." });
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return Ok(await IssueTokens(user, role));
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

        var role = await GetSingleActorRole(storedToken.User);
        if (role is null)
        {
            return Unauthorized(new { message = "Quyền tài khoản đã thay đổi. Vui lòng đăng nhập lại." });
        }

        storedToken.RevokedAtUtc = now;
        return Ok(await IssueTokens(storedToken.User, role));
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

        Response.Cookies.Delete("salon.accessToken");
        return NoContent();
    }

    private async Task<TokenResponse> IssueTokens(ApplicationUser user, string role)
    {
        var (accessToken, accessExpiry) = tokenService.CreateAccessToken(user, [role]);
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

        Response.Cookies.Append(
            "salon.accessToken",
            accessToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = new DateTimeOffset(accessExpiry),
                IsEssential = true
            });

        return new TokenResponse(accessToken, accessExpiry, refreshToken, refreshExpiry, role, user.MustChangePassword ? "/Account/ChangePassword" : GetRedirectUrl(role));
    }

    private async Task CreateAndSendEmailVerificationCodeAsync(ApplicationUser user)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        user.EmailVerificationCodeHash = userManager.PasswordHasher.HashPassword(user, code);
        user.EmailVerificationCodeExpiresAtUtc = now.AddMinutes(10);
        user.EmailVerificationCodeSentAtUtc = now;
        user.EmailVerificationFailedAttempts = 0;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Không thể lưu mã xác minh email.");
        }

        await emailService.SendEmailVerificationCodeAsync(user.Email!, code);
    }

    private async Task<UnauthorizedObjectResult> CreateLockedAccountResponseAsync(ApplicationUser user)
    {
        var lockoutEnd = await userManager.GetLockoutEndDateAsync(user);
        return Unauthorized(new
        {
            message = LockedAccountMessage,
            lockedUntilUtc = lockoutEnd?.UtcDateTime
        });
    }

    private async Task<string?> GetSingleActorRole(ApplicationUser user)
    {
        var roles = (await userManager.GetRolesAsync(user))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return roles.Length == 1 && roles[0] is
            UserRoles.Admin or UserRoles.Owner or UserRoles.Receptionist or UserRoles.Stylist
                ? roles[0]
                : null;
    }

    public static string GetRedirectUrl(string role) => role switch
    {
        UserRoles.Admin => "/admin",
        UserRoles.Owner => "/owner",
        UserRoles.Receptionist => "/reception",
        UserRoles.Stylist => "/stylist",
        _ => "/admin/login"
    };

    public static string? NormalizeStaffRole(string? role) => role?.Trim().ToLowerInvariant() switch
    {
        "reception" or "receptionist" => "Receptionist",
        "stylist" => "Stylist",
        _ => null
    };

    public static string GetInvalidCredentialsMessage(int remainingAttempts) =>
        $"{InvalidCredentialsMessage} Bạn còn {remainingAttempts} lần nhập mật khẩu trước khi tài khoản bị khóa.";

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


