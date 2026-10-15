using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class EmailVerificationTests
{
    [Fact]
    public async Task Register_ThenConfirmCode_ConfirmsEmail()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        await roleManager.CreateAsync(new IdentityRole(UserRoles.Receptionist));
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = new CapturingEmailService();
        var now = new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);
        var timeProvider = new FixedTimeProvider(now);
        var tokenService = new TokenService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Jwt:Key"] = "a-test-key-that-is-longer-than-thirty-two-characters"
        }).Build(), timeProvider);
        var controller = new AuthController(
            userManager,
            dbContext,
            tokenService,
            timeProvider,
            emailService,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var registerResult = await controller.Register(
            new RegisterStaffRequest("employee@salon.local", "password1", "reception"));

        Assert.IsType<AcceptedResult>(registerResult);
        Assert.Matches("^\\d{6}$", emailService.VerificationCode);
        var pendingUser = await userManager.FindByEmailAsync("employee@salon.local");
        Assert.NotNull(pendingUser);
        Assert.False(pendingUser!.EmailConfirmed);

        var confirmResult = await controller.ConfirmEmail(
            new ConfirmEmailRequest("employee@salon.local", emailService.VerificationCode!));

        Assert.IsType<OkObjectResult>(confirmResult);
        var confirmedUser = await userManager.FindByEmailAsync("employee@salon.local");
        Assert.True(confirmedUser!.EmailConfirmed);
        Assert.Null(confirmedUser.EmailVerificationCodeHash);
    }

    private sealed class CapturingEmailService : IEmailService
    {
        public Task SendTemporaryPasswordEmailAsync(string toEmail, string temporaryPassword) => Task.CompletedTask;
        public string? VerificationCode { get; private set; }
        public Task SendPasswordResetEmailAsync(string toEmail, string resetLink) => Task.CompletedTask;
        public Task SendEmailVerificationCodeAsync(string toEmail, string verificationCode)
        {
            VerificationCode = verificationCode;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
