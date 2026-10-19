using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class AccessRequestApprovalTests
{
    [Fact]
    public async Task Approving_a_stylist_request_creates_an_active_stylist_profile()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True((await roles.CreateAsync(new IdentityRole(UserRoles.Stylist))).Succeeded);

        var user = new ApplicationUser
        {
            UserName = "tho.moi@salon.local", Email = "tho.moi@salon.local", FullName = "Thợ Mới",
            PhoneNumber = "0912345678", EmailConfirmed = true, IsActive = false, RequestedRole = UserRoles.Stylist
        };
        Assert.True((await users.CreateAsync(user, "Password123!")).Succeeded);
        var http = new DefaultHttpContext();
        var controller = new AccessRequestsController(users, db)
        {
            TempData = new TempDataDictionary(http, new InMemoryTempDataProvider())
        };

        var result = await controller.Approve(user.Id);

        Assert.IsType<RedirectToActionResult>(result);
        var profile = await db.Stylists.SingleAsync();
        Assert.Equal("Thợ Mới", profile.FullName);
        Assert.Equal("tho.moi@salon.local", profile.Email);
        Assert.True(profile.IsActive);
        Assert.True(await users.IsInRoleAsync(user, UserRoles.Stylist));
    }

    [Fact]
    public async Task Startup_sync_creates_profiles_for_previously_approved_stylist_accounts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True((await roles.CreateAsync(new IdentityRole(UserRoles.Stylist))).Succeeded);
        var user = new ApplicationUser { UserName = "existing@salon.local", Email = "existing@salon.local", FullName = "Thợ Đã Duyệt", IsActive = true };
        Assert.True((await users.CreateAsync(user, "Password123!")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, UserRoles.Stylist)).Succeeded);

        var created = await StylistProfileSynchronizer.EnsureForActiveStylistAccountsAsync(db);

        Assert.Equal(1, created);
        Assert.Equal("Thợ Đã Duyệt", (await db.Stylists.SingleAsync()).FullName);
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
