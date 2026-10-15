using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Admin;

[Authorize(Roles = UserRoles.Admin)]
public class PermissionAuditController(IActionDescriptorCollectionProvider actions) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var rows = actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Select(action =>
            {
                var attributes = action.MethodInfo.GetCustomAttributes(true)
                    .Concat(action.ControllerTypeInfo.GetCustomAttributes(true)).ToArray();
                var isPublic = attributes.OfType<AllowAnonymousAttribute>().Any();
                var policies = attributes.OfType<AuthorizeAttribute>().ToArray();
                var rolePolicies = policies.Select(policy => (policy.Roles ?? string.Empty)
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet(StringComparer.Ordinal)).Where(policy => policy.Count > 0).ToArray();
                var isAuthenticated = policies.Length > 0;
                var states = new Dictionary<string, string>
                {
                    [UserRoles.Customer] = State(UserRoles.Customer),
                    [UserRoles.Stylist] = State(UserRoles.Stylist),
                    [UserRoles.Receptionist] = State(UserRoles.Receptionist),
                    [UserRoles.Owner] = State(UserRoles.Owner),
                    [UserRoles.Admin] = State(UserRoles.Admin),
                    ["Chưa đăng nhập"] = isPublic ? "Công khai" : !isAuthenticated ? "Không có [Authorize]" : "Yêu cầu đăng nhập"
                };
                string State(string role) => isPublic ? "Công khai" : !isAuthenticated ? "Không có [Authorize]" : rolePolicies.Length == 0 || rolePolicies.All(policy => policy.Contains(role)) ? "Cho phép" : "Từ chối";
                var route = action.AttributeRouteInfo?.Template ?? $"{action.ControllerName}/{action.ActionName}";
                return new PermissionAuditRow($"/{route.TrimStart('/')}", action.ControllerName, action.ActionName, states,
                    !isPublic && !isAuthenticated);
            })
            .OrderBy(row => row.Path).ToList();
        return View(rows);
    }
}

public sealed record PermissionAuditRow(string Path, string Controller, string Action, IReadOnlyDictionary<string, string> Roles, bool MissingAuthorization);
