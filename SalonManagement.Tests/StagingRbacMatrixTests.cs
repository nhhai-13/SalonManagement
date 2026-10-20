using Microsoft.AspNetCore.Authorization;
using SalonManagement.Controllers;
using SalonManagement.Controllers.Booking;
using SalonManagement.Controllers.Owner;
using SalonManagement.Controllers.Reception;
using SalonManagement.Controllers.Reviews;
using SalonManagement.Controllers.StylistPortal;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

/// <summary>Regression checks for the protected surfaces exercised in the staging runbook.</summary>
public sealed class StagingRbacMatrixTests
{
    public static IEnumerable<object[]> ProtectedControllers()
    {
        yield return [new ControllerRoleCase(typeof(CounterAppointmentsController), [UserRoles.Receptionist])];
        yield return [new ControllerRoleCase(typeof(PendingAppointmentsController), [UserRoles.Receptionist])];
        yield return [new ControllerRoleCase(typeof(AppointmentEmailNotificationsController), [UserRoles.Receptionist])];
        yield return [new ControllerRoleCase(typeof(ReceptionDailyScheduleController), [UserRoles.Receptionist, UserRoles.Owner])];
        yield return [new ControllerRoleCase(typeof(StylistCommandCenterController), [UserRoles.Stylist])];
        yield return [new ControllerRoleCase(typeof(SalonManagement.Controllers.StylistPortal.StylistNotificationsController), [UserRoles.Stylist])]; // BUG-01 regression
        yield return [new ControllerRoleCase(typeof(StylistScheduleController), [UserRoles.Stylist])];
        yield return [new ControllerRoleCase(typeof(StylistLeaveRequestsController), [UserRoles.Stylist])];
        yield return [new ControllerRoleCase(typeof(OwnerOperationsDashboardController), [UserRoles.Owner])];
        yield return [new ControllerRoleCase(typeof(RevenueReportController), [UserRoles.Owner])];
        yield return [new ControllerRoleCase(typeof(PeakHoursReportController), [UserRoles.Owner])];
        yield return [new ControllerRoleCase(typeof(PromotionCodesController), [UserRoles.Owner])];
        yield return [new ControllerRoleCase(typeof(ServiceReviewReportsController), [UserRoles.Owner])];
    }

    [Theory]
    [MemberData(nameof(ProtectedControllers))]
    public void Staging_protected_controller_has_exactly_the_expected_roles(ControllerRoleCase testCase)
    {
        var roles = testCase.Controller.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .SelectMany(attribute => (attribute.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role)
            .ToArray();

        Assert.Equal(testCase.ExpectedRoles.OrderBy(role => role), roles);
    }

    [Theory]
    [InlineData(typeof(BookingController))]
    [InlineData(typeof(AppointmentLookupController))]
    [InlineData(typeof(StylistAvailabilityController))]
    [InlineData(typeof(ServiceReviewsController))]
    public void Customer_facing_controller_is_explicitly_anonymous(Type controller)
    {
        Assert.NotEmpty(controller.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    [Fact]
    public void All_demo_roles_exist_in_the_authorization_role_catalog()
    {
        var roles = new[] { UserRoles.Customer, UserRoles.Receptionist, UserRoles.Stylist, UserRoles.Owner };
        Assert.All(roles, role => Assert.False(string.IsNullOrWhiteSpace(role)));
        Assert.Equal(4, roles.Distinct(StringComparer.Ordinal).Count());
    }

    public sealed record ControllerRoleCase(Type Controller, string[] ExpectedRoles);
}
