using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SalonManagement.Controllers;
using SalonManagement.Data.Migrations;
using Xunit;
namespace SalonManagement.Tests;
public class ReschedulingCompatibilityTests
{
    [Fact]
    public void ExplicitControllerEndpointsHaveNoDuplicateVerbAndRoute()
    {
        var endpoints = new List<string>();
        foreach (var type in typeof(ReschedulingPageController).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)))
        {
            var prefixes = type.GetCustomAttributes<RouteAttribute>().Select(a => a.Template).DefaultIfEmpty("");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var attribute in method.GetCustomAttributes<HttpMethodAttribute>())
                    foreach (var prefix in prefixes)
                        foreach (var verb in attribute.HttpMethods)
                        {
                            var template = attribute.Template ?? "";
                            if (string.IsNullOrEmpty(prefix) && attribute.Template is null) continue;
                            var route = template.StartsWith('/') || template.StartsWith("~/") ? template.TrimStart('~', '/') : (prefix.Trim('/') + "/" + template.Trim('/')).Trim('/');
                            route = route.Replace("[controller]", type.Name.Replace("Controller", "")).Replace("[action]", method.Name);
                            endpoints.Add(verb + " " + route.ToLowerInvariant());
                        }
        }
        Assert.Empty(endpoints.GroupBy(e => e).Where(g => g.Count() > 1).Select(g => g.Key));
    }
    [Fact]
    public void LegacyHistoryUpgradeRetainsRowsAndFreesOriginalConstraintNames()
    {
        var migration = new PreserveRescheduleHistory { ActiveProvider = "Microsoft.EntityFrameworkCore.SqlServer" };
        Assert.DoesNotContain(migration.UpOperations, operation => operation is DropTableOperation or DeleteDataOperation);
        var upgrade = string.Join("\n", migration.UpOperations.OfType<SqlOperation>().Select(o => o.Sql));
        Assert.Contains("sp_rename", upgrade);
        Assert.Contains("ChangedAtUtc", upgrade);
        Assert.Contains("PK_RescheduleChangeLogs", upgrade);
        Assert.Contains("FK_RescheduleChangeLogs_Appointments_AppointmentId", upgrade);
    }
}