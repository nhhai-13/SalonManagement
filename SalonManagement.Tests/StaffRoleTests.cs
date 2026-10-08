using SalonManagement.Controllers;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public class StaffRoleTests
{
    [Theory]
    [InlineData(UserRoles.Admin, "/admin")]
    [InlineData(UserRoles.Owner, "/owner")]
    [InlineData(UserRoles.Receptionist, "/reception")]
    [InlineData(UserRoles.Stylist, "/stylist")]
    public void LoginRedirect_ShouldFollowAccountRole(string role, string expected)
    {
        Assert.Equal(expected, AuthController.GetRedirectUrl(role));
    }

    [Theory]
    [InlineData("reception", "Receptionist")]
    [InlineData("Receptionist", "Receptionist")]
    [InlineData("stylist", "Stylist")]
    public void NormalizeStaffRole_AllowsOnlyStaffRoles(string input, string expected)
    {
        Assert.Equal(expected, AuthController.NormalizeStaffRole(input));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    [InlineData("Customer")]
    public void NormalizeStaffRole_RejectsUnauthorizedRoles(string input)
    {
        Assert.Null(AuthController.NormalizeStaffRole(input));
    }
}
