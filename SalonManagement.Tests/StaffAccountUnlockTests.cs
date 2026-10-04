using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class StaffAccountUnlockTests
{
    [Fact]
    public async Task UnlockAsync_LockedAccount_ClearsLockoutAndFailedAttempts()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>(options =>
            options.Lockout.MaxFailedAccessAttempts = 5)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new ApplicationUser
        {
            UserName = "locked@salon.local",
            Email = "locked@salon.local",
            LockoutEnabled = true,
            LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15),
            AccessFailedCount = 5
        };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        var service = new StaffAccountService(userManager, dbContext, TimeProvider.System);

        var result = await service.UnlockAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal("Đã mở khóa tài khoản. Người dùng có thể đăng nhập lại ngay.", result.Message);
        var updatedUser = await userManager.FindByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Null(updatedUser!.LockoutEnd);
        Assert.Equal(0, updatedUser.AccessFailedCount);
        Assert.False(await userManager.IsLockedOutAsync(updatedUser));
    }
}
