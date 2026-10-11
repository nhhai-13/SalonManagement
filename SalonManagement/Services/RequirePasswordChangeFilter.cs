using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SalonManagement.Services;

public sealed class RequirePasswordChangeFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.FindFirst("must_change_password")?.Value != "true") return;
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();
        if (controller == "Account" && action is "ChangePassword" or "ForgotPassword" or "ForgotPasswordConfirmation" or "ResetPassword" or "ResetPasswordConfirmation" or "Login") return;
        if (controller == "Auth" && action is "Login" or "Logout" or "Refresh") return;
        context.Result = context.HttpContext.Request.Path.StartsWithSegments("/api")
            ? new ObjectResult(new { message = "Bạn phải đổi mật khẩu trước khi sử dụng chức năng này.", redirectUrl = "/Account/ChangePassword" }) { StatusCode = 403 }
            : new RedirectToActionResult("ChangePassword", "Account", null);
    }
}
