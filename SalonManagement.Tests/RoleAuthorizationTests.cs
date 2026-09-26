using Microsoft.AspNetCore.Authorization;
using SalonManagement.Controllers;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public class RoleAuthorizationTests
{
    [Fact]
    public void AdminApiController_ShouldRequireAdminRole()
    {
        var attribute = typeof(AdminApiController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(UserRoles.Admin, attribute!.Roles);
    }

    [Fact]
    public void BusinessHoursController_ShouldRequireManagementRoles()
    {
        var attribute = typeof(BusinessHoursController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(RoleGroups.Management, attribute!.Roles);
    }

    [Fact]
    public void ManagementRoleGroup_ShouldContainAdminAndOwner()
    {
        var roles = RoleGroups.Management.Split(',');

        Assert.Contains(UserRoles.Admin, roles);
        Assert.Contains(UserRoles.Owner, roles);

        Assert.DoesNotContain(UserRoles.Receptionist, roles);
        Assert.DoesNotContain(UserRoles.Stylist, roles);
        Assert.DoesNotContain(UserRoles.Customer, roles);
    }
}