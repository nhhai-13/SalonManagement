using Microsoft.AspNetCore.Authorization;
using SalonManagement.Controllers;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public class RoleAuthorizationTests
{
    [Theory]
    [InlineData(nameof(AdminController.Index))]
    [InlineData(nameof(AdminController.BusinessHours))]
    [InlineData(nameof(AdminController.Owner))]
    public void AdminPageShells_ShouldAllowJwtClientToLoad(string actionName)
    {
        var action = typeof(AdminController).GetMethod(actionName);
        var attribute = action!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.Null(attribute);
    }

    [Theory]
    [InlineData(nameof(StaffPortalApiController.ReceptionSession), UserRoles.Receptionist)]
    [InlineData(nameof(StaffPortalApiController.StylistSession), UserRoles.Stylist)]
    public void StaffPortalSessions_ShouldRequireTheirExactRole(string actionName, string role)
    {
        var action = typeof(StaffPortalApiController).GetMethod(actionName);
        var attribute = action!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(role, attribute!.Roles);
    }

    [Fact]
    public void AdminStaffAccountApi_ShouldRequireAdminRole()
    {
        var attribute = typeof(AdminApiController)
            .GetMethod(nameof(AdminApiController.CreateStaffAccount))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(UserRoles.Admin, attribute!.Roles);
    }

    [Fact]
    public void BusinessHoursController_ShouldRequireOwnerRole()
    {
        var attribute = typeof(BusinessHoursController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(UserRoles.Owner, attribute!.Roles);
    }

    [Theory]
    [InlineData(nameof(ServicesController.Create), 0)]
    [InlineData(nameof(ServicesController.Create), 1)]
    [InlineData(nameof(ServicesController.Edit), 0)]
    [InlineData(nameof(ServicesController.Edit), 1)]
    [InlineData(nameof(ServicesController.ToggleStatus), 0)]
    [InlineData(nameof(ServicesController.Delete), 0)]
    public void ServiceManagementActions_ShouldRequireOwnerRole(string actionName, int overloadIndex)
    {
        var action = typeof(ServicesController)
            .GetMethods()
            .Where(method => method.Name == actionName)
            .OrderBy(method => method.GetParameters().Length)
            .ElementAt(overloadIndex);
        var attribute = action
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(UserRoles.Owner, attribute!.Roles);
    }

    [Fact]
    public void AdminSession_ShouldRequireAdminRole()
    {
        var attribute = typeof(AdminApiController)
            .GetMethod(nameof(AdminApiController.Session))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(UserRoles.Admin, attribute!.Roles);
    }

    [Fact]
    public void OwnerApiController_ShouldRequireOwnerRole()
    {
        var attribute = typeof(OwnerApiController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(UserRoles.Owner, attribute!.Roles);
    }

    [Fact]
    public void AuditLogsController_ShouldRequireAdminRole()
    {
        var attribute = typeof(AuditLogsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(UserRoles.Admin, attribute!.Roles);
    }

    [Fact]
    public void StaffController_ShouldRequireAdminRole()
    {
        var attribute = typeof(StaffController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attribute);
        Assert.Equal(UserRoles.Admin, attribute!.Roles);
    }
}
