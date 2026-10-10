using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using Xunit;

namespace SalonManagement.Tests;

public class ServiceGroupsControllerTests
{
    [Fact]
    public async Task CreateAndEdit_PersistTrimmedNameAndOrder()
    {
        await using var db = CreateDb();
        var controller = Controller(db);
        Assert.IsType<RedirectToActionResult>(await controller.Create(new() { GroupName = "  Tóc  ", DisplayOrder = 3 }));
        var group = await db.ServiceGroups.SingleAsync();
        Assert.Equal("Tóc", group.GroupName);
        Assert.IsType<RedirectToActionResult>(await controller.Edit(group.ServiceGroupId,
            new() { ServiceGroupId = group.ServiceGroupId, GroupName = "Gội", DisplayOrder = 1 }));
        db.ChangeTracker.Clear();
        group = await db.ServiceGroups.SingleAsync();
        Assert.Equal("Gội", group.GroupName);
        Assert.Equal(1, group.DisplayOrder);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("Tóc", -1)]
    public async Task Create_RejectsInvalidInput(string name, int order)
    {
        await using var db = CreateDb();
        var controller = Controller(db);
        Assert.IsType<ViewResult>(await controller.Create(new() { GroupName = name, DisplayOrder = order }));
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(db.ServiceGroups);
    }

    [Fact]
    public async Task Edit_RejectsLongNameWithoutChangingGroup()
    {
        await using var db = CreateDb();
        var group = new ServiceGroup { GroupName = "Tóc" };
        db.Add(group);
        await db.SaveChangesAsync();
        var controller = Controller(db);
        Assert.IsType<ViewResult>(await controller.Edit(group.ServiceGroupId,
            new() { ServiceGroupId = group.ServiceGroupId, GroupName = new string('a', 101) }));
        Assert.Equal("Tóc", group.GroupName);
    }

    [Fact]
    public async Task Delete_BlocksActiveServicesAndReportsCounts()
    {
        await using var db = CreateDb();
        var group = new ServiceGroup { GroupName = "Tóc", Services = [new() { IsActive = true }, new() { IsActive = false }] };
        db.Add(group);
        await db.SaveChangesAsync();
        var controller = Controller(db);
        await controller.Delete(group.ServiceGroupId);
        Assert.Single(db.ServiceGroups);
        Assert.Equal(2, await db.Services.CountAsync());
        var error = Assert.IsType<string>(controller.TempData["Error"]);
        Assert.Contains("2 dịch vụ thuộc nhóm", error);
        Assert.Contains("1 dịch vụ đang bán", error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_AllowsEmptyOrInactiveGroupAndPreservesServices(bool hasInactive)
    {
        await using var db = CreateDb();
        var group = new ServiceGroup { GroupName = "Tóc" };
        if (hasInactive) group.Services.Add(new() { ServiceName = "Cắt", IsActive = false });
        db.Add(group);
        await db.SaveChangesAsync();
        await Controller(db).Delete(group.ServiceGroupId);
        db.ChangeTracker.Clear();
        Assert.Empty(db.ServiceGroups);
        Assert.Equal(hasInactive ? 1 : 0, await db.Services.CountAsync());
        if (hasInactive) Assert.Null((await db.Services.SingleAsync()).ServiceGroupId);
    }

    [Fact]
    public async Task MissingAndMismatchedIds_ReturnNotFound()
    {
        await using var db = CreateDb();
        var controller = Controller(db);
        Assert.IsType<NotFoundResult>(await controller.Edit(123));
        Assert.IsType<NotFoundResult>(await controller.Edit(123, new() { ServiceGroupId = 124 }));
        Assert.IsType<NotFoundResult>(await controller.Edit(123, new() { ServiceGroupId = 123 }));
        Assert.IsType<NotFoundResult>(await controller.Delete(123));
    }

    [Fact]
    public async Task PublicCatalog_UsesNewGroupOrderWhileHomeKeepsActiveServices()
    {
        await using var db = CreateDb();
        var first = new ServiceGroup { GroupName = "Tóc", DisplayOrder = 0, Services = [new() { ServiceName = "Z" }] };
        var second = new ServiceGroup { GroupName = "Gội", DisplayOrder = 1, Services = [new() { ServiceName = "A" }, new() { ServiceName = "Ẩn", IsActive = false }] };
        db.AddRange(first, second);
        db.Services.Add(new() { ServiceName = "Chưa phân nhóm" });
        var stylist = new Stylist { FullName = "Thợ đang làm việc" };
        foreach (var service in db.ChangeTracker.Entries<Service>().Select(entry => entry.Entity).ToList())
            service.Stylists.Add(new StylistService { Service = service, Stylist = stylist });
        await db.SaveChangesAsync();
        var services = new ServicesController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        async Task<string[]> PublicNames()
        {
            var result = Assert.IsType<ViewResult>(await services.Index());
            Assert.Equal("Public", result.ViewName);
            return Assert.IsType<PublicServiceCatalog>(result.Model).Groups.SelectMany(g => g.Services).Select(s => s.ServiceName).ToArray();
        }
        Assert.Equal(new[] { "Z", "A", "Chưa phân nhóm" }, await PublicNames());
        await Controller(db).Edit(second.ServiceGroupId, new() { ServiceGroupId = second.ServiceGroupId, GroupName = "Gội", DisplayOrder = 0 });
        await Controller(db).Edit(first.ServiceGroupId, new() { ServiceGroupId = first.ServiceGroupId, GroupName = "Tóc", DisplayOrder = 2 });
        Assert.Equal(new[] { "A", "Z", "Chưa phân nhóm" }, await PublicNames());
        var home = new HomeController(NullLogger<HomeController>.Instance, db);
        var model = Assert.IsType<HomeViewModel>(Assert.IsType<ViewResult>(await home.Index()).Model);
        Assert.Equal(new[] { "A", "Z", "Chưa phân nhóm" }, model.Services.Select(s => s.ServiceName));
    }

    [Fact]
    public void Management_RequiresOwnerAndAntiforgeryOnEveryMutation()
    {
        var type = typeof(ServiceGroupsController);
        Assert.Equal(UserRoles.Owner, Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()).Roles);
        var mutations = type.GetMethods().Where(m => m.IsDefined(typeof(HttpPostAttribute), true)).ToList();
        Assert.Equal(3, mutations.Count);
        Assert.All(mutations, m => Assert.True(m.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true)));
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new HttpContextAccessor());

    private static ServiceGroupsController Controller(ApplicationDbContext db)
    {
        var http = new DefaultHttpContext();
        return new(db) { ControllerContext = new() { HttpContext = http }, TempData = new TempDataDictionary(http, new MemoryTempDataProvider()) };
    }

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
