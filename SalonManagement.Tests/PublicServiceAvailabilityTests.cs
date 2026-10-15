using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using Xunit;

namespace SalonManagement.Tests;

public class PublicServiceAvailabilityTests
{
    [Theory]
    [InlineData(true, 1, 0, true)]
    [InlineData(false, 1, 0, false)]
    [InlineData(true, 0, 0, false)]
    [InlineData(true, 0, 2, false)]
    [InlineData(true, 1, 2, true)]
    public async Task PublicPages_OnlyShowAvailableServicesAndTheirGroups(
        bool selling, int activeStylists, int inactiveStylists, bool visible)
    {
        await using var db = CreateDb();
        var service = new Service
        {
            ServiceName = "Cắt tóc", IsActive = selling,
            ServiceGroup = new ServiceGroup { GroupName = "Tóc" }
        };
        for (var i = 0; i < activeStylists + inactiveStylists; i++)
            service.Stylists.Add(new() { Stylist = new Stylist { IsActive = i < activeStylists } });
        db.Add(service);
        await db.SaveChangesAsync();
        await AssertPublicVisibility(db, visible);

        var owner = new ServicesController(db)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, UserRoles.Owner)], "test"))
            } }
        };
        var result = Assert.IsType<ViewResult>(await owner.Index());
        Assert.Equal("Index", result.ViewName);
        Assert.Single(Assert.IsAssignableFrom<IEnumerable<Service>>(result.Model));
    }

    [Fact]
    public async Task PublicPages_ReflectServiceAndStylistStatusAndAssignmentChanges()
    {
        await using var db = CreateDb();
        var stylist = new Stylist();
        var service = new Service { ServiceGroup = new ServiceGroup { GroupName = "Tóc" } };
        var link = new StylistService { Stylist = stylist, Service = service };
        db.Add(link);
        await db.SaveChangesAsync();
        await AssertPublicVisibility(db, true);
        service.IsActive = false;
        await db.SaveChangesAsync();
        await AssertPublicVisibility(db, false);
        service.IsActive = true;
        await db.SaveChangesAsync();
        await AssertPublicVisibility(db, true);
        stylist.IsActive = false;
        await db.SaveChangesAsync();
        await AssertPublicVisibility(db, false);
        stylist.IsActive = true;
        await db.SaveChangesAsync();
        await AssertPublicVisibility(db, true);
        db.Remove(link);
        await db.SaveChangesAsync();
        await AssertPublicVisibility(db, false);
    }

    private static async Task AssertPublicVisibility(ApplicationDbContext db, bool visible)
    {
        var controller = new ServicesController(db)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
        var result = Assert.IsType<ViewResult>(await controller.Index());
        Assert.Equal("Public", result.ViewName);
        var catalog = Assert.IsType<PublicServiceCatalog>(result.Model);
        Assert.Equal(visible ? 1 : 0, catalog.Groups.Count);
        var services = catalog.Groups.SelectMany(group => group.Services).ToList();
        var home = new HomeController(NullLogger<HomeController>.Instance, db);
        var model = Assert.IsType<HomeViewModel>(Assert.IsType<ViewResult>(await home.Index()).Model);
        foreach (var list in new[] { services, model.Services })
        {
            Assert.Equal(visible ? 1 : 0, list.Count);
            Assert.Equal(visible ? 1 : 0, list.GroupBy(s => s.ServiceGroupId).Count());
        }
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());
}
