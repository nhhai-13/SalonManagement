using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class LoginLockoutTests
{
    [Fact]
    public async Task SixthWrongPasswordLocksAccountAndBlocksCorrectPassword()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor(); services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>(o => {
            o.Lockout.MaxFailedAccessAttempts = LoginLockoutPolicy.MaxFailedAttempts;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<ApplicationDbContext>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new ApplicationUser { UserName = "lockout@test.local", Email = "lockout@test.local", IsActive = true, EmailConfirmed = true, LockoutEnabled = true };
        Assert.True((await manager.CreateAsync(user, "ValidPassword123!")).Succeeded);
        var controller = new AuthController(manager, db, null!, TimeProvider.System, null!, NullLogger<AuthController>.Instance);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            Assert.IsType<UnauthorizedObjectResult>((await controller.Login(new(user.Email!, "wrong"))).Result);
            Assert.False(await manager.IsLockedOutAsync(user));
            Assert.Equal(attempt, await manager.GetAccessFailedCountAsync(user));
        }
        Assert.IsType<UnauthorizedObjectResult>((await controller.Login(new(user.Email!, "wrong"))).Result);
        Assert.True(await manager.IsLockedOutAsync(user));
        Assert.IsType<UnauthorizedObjectResult>((await controller.Login(new(user.Email!, "ValidPassword123!"))).Result);
        Assert.True(await manager.IsLockedOutAsync(user));
    }
}
