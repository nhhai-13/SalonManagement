using SalonManagement.Controllers;
using Xunit;

namespace SalonManagement.Tests;

public class StaffRoleTests
{
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
