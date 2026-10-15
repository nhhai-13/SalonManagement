using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Controllers;
using Xunit;

namespace SalonManagement.Tests;

public class PermissionAuditCoverageTests
{
    [Fact]
    public void EveryMvcActionMustDeclareAuthorizationOrBeExplicitlyPublic()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllersWithViews().AddApplicationPart(typeof(AdminController).Assembly);
        using var provider = services.BuildServiceProvider();

        var actions = provider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>();

        var missing = actions.Where(action =>
        {
            var attributes = action.MethodInfo.GetCustomAttributes(true)
                .Concat(action.ControllerTypeInfo.GetCustomAttributes(true)).ToArray();
            return !attributes.OfType<AllowAnonymousAttribute>().Any()
                && !attributes.OfType<AuthorizeAttribute>().Any();
        }).Select(action => $"{action.ControllerName}.{action.ActionName}").Distinct().Order().ToArray();

        Assert.True(missing.Length == 0,
            "MVC actions must declare [Authorize] or [AllowAnonymous]: " + string.Join(", ", missing));
    }
}
