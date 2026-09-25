using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using SalonManagement.Services;

namespace SalonManagement.Controllers;

/// <summary>
/// Xử lý các luồng quản lý mật khẩu: Đổi mật khẩu (AC1),
/// Quên/Đặt lại mật khẩu (AC2), Thu hồi phiên (AC3), Rate limiting (AC4).
/// </summary>
public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext dbContext,
    IEmailService emailService,
    IPasswordResetRateLimiter rateLimiter,
    TimeProvider timeProvider,
    ILogger<AccountController> logger) : Controller
{
    // ═══════════════════════════════════════════════════════════════
    // AC1: Đổi mật khẩu (yêu cầu đăng nhập)
    // ═══════════════════════════════════════════════════════════════

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

        // Đổi mật khẩu qua UserManager — xác nhận OldPassword trước khi hash mới
        var result = await userManager.ChangePasswordAsync(
            user, model.CurrentPassword, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // AC3: Sau khi đổi mật khẩu thành công — thu hồi toàn bộ Refresh Tokens cũ
        await RevokeAllRefreshTokensAsync(user.Id);

        // Cập nhật SecurityStamp để vô hiệu hoá mọi cookie/session cũ trên thiết bị khác
        await userManager.UpdateSecurityStampAsync(user);

        logger.LogInformation(
            "Người dùng {UserId} đã đổi mật khẩu thành công.", user.Id);

        // Đăng xuất phiên hiện tại rồi yêu cầu đăng nhập lại với mật khẩu mới
        await signInManager.SignOutAsync();

        TempData["SuccessMessage"] =
            "Đổi mật khẩu thành công! Vui lòng đăng nhập lại bằng mật khẩu mới.";
        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }

    // ═══════════════════════════════════════════════════════════════
    // AC2 + AC4: Quên mật khẩu
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // AC4: Kiểm tra rate limit TRƯỚC khi tra cứu user (tránh user enumeration)
        if (!rateLimiter.TryRecord(model.Email))
        {
            logger.LogWarning(
                "Vượt quá giới hạn yêu cầu đặt lại mật khẩu cho email {Email}.", model.Email);
            ModelState.AddModelError(string.Empty,
                "Bạn đã yêu cầu đặt lại mật khẩu quá số lần cho phép (tối đa 3 lần/giờ). " +
                "Vui lòng thử lại sau.");
            return View(model);
        }

        // Luôn chuyển sang trang xác nhận để tránh lộ thông tin email có tồn tại không
        // (Phòng User Enumeration Attack)
        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is not null && user.IsActive)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);

            // Mã hoá token an toàn khi truyền qua URL
            var encodedToken = Uri.EscapeDataString(token);
            var resetLink = Url.Action(
                nameof(ResetPassword), "Account",
                new { email = model.Email, token = encodedToken },
                Request.Scheme)!;

            await emailService.SendPasswordResetEmailAsync(model.Email, resetLink);

            logger.LogInformation(
                "Đã gửi link đặt lại mật khẩu tới {Email}.", model.Email);
        }
        else
        {
            logger.LogWarning(
                "Yêu cầu đặt lại mật khẩu cho email không tồn tại hoặc bị vô hiệu hoá: {Email}.",
                model.Email);
        }

        // Luôn trả về trang xác nhận dù email có tồn tại hay không
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // AC2 + AC3: Đặt lại mật khẩu
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (email is null || token is null)
        {
            return BadRequest("Link đặt lại mật khẩu không hợp lệ.");
        }

        var model = new ResetPasswordViewModel
        {
            Email = email,
            Token = Uri.UnescapeDataString(token)
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            // Tránh lộ user enumeration — vẫn redirect sang trang thành công
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        var result = await userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            // Thêm thông báo cụ thể nếu token hết hạn / đã dùng
            if (result.Errors.Any(e => e.Code is "InvalidToken"))
            {
                ModelState.AddModelError(string.Empty,
                    "Link đặt lại mật khẩu đã hết hạn hoặc đã được sử dụng. " +
                    "Vui lòng yêu cầu link mới.");
            }
            return View(model);
        }

        // AC3: Thu hồi toàn bộ Refresh Tokens sau khi đặt lại mật khẩu thành công
        await RevokeAllRefreshTokensAsync(user.Id);

        // SecurityStamp đã được ResetPasswordAsync cập nhật tự động,
        // nhưng gọi thêm để đảm bảo vô hiệu hoá mọi phiên cookie cũ
        await userManager.UpdateSecurityStampAsync(user);

        logger.LogInformation(
            "Người dùng {UserId} đã đặt lại mật khẩu thành công.", user.Id);

        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // Helper: Thu hồi toàn bộ Refresh Tokens của user (AC3)
    // ═══════════════════════════════════════════════════════════════

    private async Task RevokeAllRefreshTokensAsync(string userId)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync();

        if (activeTokens.Count == 0)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Đã thu hồi {Count} refresh token của người dùng {UserId}.",
            activeTokens.Count, userId);
    }
}
