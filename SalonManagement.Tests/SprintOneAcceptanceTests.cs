using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class SprintOneAcceptanceTests
{
    [Fact]
    public async Task SeedTwice_HasFiveRolesFourAccountsFiveStylistsTwelveServicesWithoutDuplicates()
    {
        await using var fixture = new Fixture();
        await RoleSeeder.SeedRolesAsync(fixture.Roles);
        await SeedData.SeedAsync(fixture.Db, fixture.Users);
        await SeedData.SeedAsync(fixture.Db, fixture.Users);
        Assert.Equal(5, await fixture.Db.Roles.CountAsync());
        Assert.Equal(4, await fixture.Db.Users.CountAsync());
        Assert.Equal(5, await fixture.Db.Stylists.CountAsync());
        Assert.Equal(12, await fixture.Db.Services.CountAsync());
        Assert.Equal(3, await fixture.Db.ServiceGroups.CountAsync());
        Assert.All(await fixture.Db.Services.ToListAsync(), service => Assert.InRange(service.DurationMinutes, 30, 120));
        foreach (var role in new[] { "Admin", "Owner", "Receptionist", "Stylist" })
        {
            var user = Assert.Single(await fixture.Users.GetUsersInRoleAsync(role));
            Assert.True(user.IsActive && user.EmailConfirmed);
            Assert.Single(await fixture.Users.GetRolesAsync(user));
        }
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Owner)]
    [InlineData(UserRoles.Receptionist)]
    [InlineData(UserRoles.Stylist)]
    public async Task CreateStaff_GeneratesEmailedTemporaryPasswordAndCapturesProfile(string role)
    {
        await using var f = new Fixture();
        await RoleSeeder.SeedRolesAsync(f.Roles);
        var mail = new CapturingEmail();
        var controller = new AdminApiController(f.Users, mail, NullLogger<AdminApiController>.Instance)
        { ControllerContext = new() { HttpContext = f.Http } };
        var request = new CreateStaffAccountRequest("new@salon.local", " Nhân viên ", role, "0901234567");
        Assert.IsType<CreatedResult>(await controller.CreateStaffAccount(request));
        var user = (await f.Users.FindByEmailAsync(request.Email))!;
        Assert.Equal("Nhân viên", user.FullName);
        Assert.Equal("0901234567", user.PhoneNumber);
        Assert.Equal("actor", user.CreatedByUserId);
        Assert.True(user.MustChangePassword);
        Assert.True(await f.Users.CheckPasswordAsync(user, mail.Password!));
        Assert.Equal(role, Assert.Single(await f.Users.GetRolesAsync(user)));
        Assert.IsType<ConflictObjectResult>(await controller.CreateStaffAccount(request));
    }

    [Fact]
    public async Task CreateStaff_EmailFailureDoesNotLeaveUsableAccount()
    {
        await using var f = new Fixture();
        await RoleSeeder.SeedRolesAsync(f.Roles);
        var controller = new AdminApiController(f.Users, new CapturingEmail { Fail = true }, NullLogger<AdminApiController>.Instance)
        { ControllerContext = new() { HttpContext = f.Http } };
        var result = Assert.IsType<ObjectResult>(await controller.CreateStaffAccount(new("new@salon.local", "Tên", UserRoles.Stylist, "0901234567")));
        Assert.Equal(503, result.StatusCode);
        Assert.Null(await f.Users.FindByEmailAsync("new@salon.local"));
    }

    [Fact]
    public async Task RoleChange_AffectsSamePrincipalOnNextRequestWithoutInvalidatingSession()
    {
        await using var f = new Fixture();
        var user = await f.CreateUser(UserRoles.Stylist);
        var principal = Principal(user, UserRoles.Stylist);
        var service = new StaffAccountService(f.Users, f.Db, TimeProvider.System);
        Assert.True((await service.UpdateAsync(user.Id, new("Tên mới", user.Email!, "0901234567", UserRoles.Owner))).Success);
        Assert.Equal("Tên mới", user.FullName);
        Assert.True(await new SessionPrincipalValidator(f.Users).ValidateAsync(principal));
        Assert.True(principal.IsInRole(UserRoles.Owner));
        Assert.False(principal.IsInRole(UserRoles.Stylist));
        Assert.Equal(user.Id, principal.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task Deactivate_ImmediatelyRejectsPrincipalAndRevokesRefreshTokens()
    {
        await using var f = new Fixture();
        var user = await f.CreateUser(UserRoles.Stylist);
        var principal = Principal(user, UserRoles.Stylist);
        var token = new RefreshToken { UserId = user.Id, TokenHash = "test", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        f.Db.Add(token);
        await f.Db.SaveChangesAsync();
        Assert.True((await new StaffAccountService(f.Users, f.Db, TimeProvider.System).ChangeStatusAsync(user.Id, false)).Success);
        Assert.NotNull(token.RevokedAtUtc);
        Assert.False(await new SessionPrincipalValidator(f.Users).ValidateAsync(principal));
    }

    [Fact]
    public async Task LastAdmin_CannotBeDeactivatedOrDemoted()
    {
        await using var f = new Fixture();
        var user = await f.CreateUser(UserRoles.Admin);
        var service = new StaffAccountService(f.Users, f.Db, TimeProvider.System);
        Assert.False((await service.ChangeStatusAsync(user.Id, false)).Success);
        Assert.False((await service.UpdateAsync(user.Id, new("Tên", user.Email!, "0901234567", UserRoles.Owner))).Success);
        Assert.True(user.IsActive);
        Assert.True(await f.Users.IsInRoleAsync(user, UserRoles.Admin));
    }

    [Fact]
    public async Task ASecondAdmin_CanBeDeactivatedOrDemoted()
    {
        await using var f = new Fixture();
        var first = await f.CreateUser(UserRoles.Admin);
        var second = new ApplicationUser { UserName = "second@salon.local", Email = "second@salon.local", EmailConfirmed = true };
        Assert.True((await f.Users.CreateAsync(second, "Password123!")).Succeeded);
        Assert.True((await f.Users.AddToRoleAsync(second, UserRoles.Admin)).Succeeded);
        var service = new StaffAccountService(f.Users, f.Db, TimeProvider.System);
        Assert.True((await service.ChangeStatusAsync(second.Id, false)).Success);
        Assert.True((await service.UpdateAsync(second.Id, new("Tên", second.Email!, "0901234567", UserRoles.Owner))).Success);
        Assert.True(await f.Users.IsInRoleAsync(first, UserRoles.Admin));
        Assert.True(await f.Users.IsInRoleAsync(second, UserRoles.Owner));
    }

    [Theory]
    [InlineData("Services", "Create", "/Services/Create", false)]
    [InlineData("AdminApi", "CreateStaffAccount", "/api/admin/staff-accounts", false)]
    [InlineData("Account", "ChangePassword", "/Account/ChangePassword", true)]
    public void FirstPasswordChange_IsEnforcedBeforeOtherActions(string controller, string action, string path, bool allowed)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new("must_change_password", "true")], "test")) };
        http.Request.Path = path;
        var route = new RouteData(); route.Values["controller"] = controller; route.Values["action"] = action;
        var context = new AuthorizationFilterContext(new ActionContext(http, route, new ActionDescriptor()), []);
        new RequirePasswordChangeFilter().OnAuthorization(context);
        if (allowed) Assert.Null(context.Result);
        else if (path.StartsWith("/api")) Assert.Equal(403, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        else Assert.IsType<RedirectToActionResult>(context.Result);
    }

    [Fact]
    public async Task ResetPassword_ConsumesTokenAndRevokesOldSessionsAndFirstLoginFlag()
    {
        await using var f = new Fixture();
        var user = await f.CreateUser(UserRoles.Stylist);
        user.MustChangePassword = true;
        await f.Users.UpdateAsync(user);
        var oldPrincipal = Principal(user, UserRoles.Stylist);
        var token = await f.Users.GeneratePasswordResetTokenAsync(user);
        var refresh = new RefreshToken { UserId = user.Id, TokenHash = "refresh", ExpiresAtUtc = DateTime.UtcNow.AddDays(1) };
        f.Db.Add(refresh); await f.Db.SaveChangesAsync();
        var controller = new AccountController(f.Users, null!, f.Db, new CapturingEmail(), null!, TimeProvider.System, NullLogger<AccountController>.Instance)
        { ControllerContext = new() { HttpContext = f.Http } };
        Assert.IsType<RedirectToActionResult>(await controller.ResetPassword(new ResetPasswordViewModel { Email = user.Email!, Token = token, NewPassword = "Newpass123!", ConfirmPassword = "Newpass123!" }));
        Assert.False(user.MustChangePassword);
        Assert.NotNull(refresh.RevokedAtUtc);
        Assert.False(await new SessionPrincipalValidator(f.Users).ValidateAsync(oldPrincipal));
        Assert.False((await f.Users.ResetPasswordAsync(user, token, "Another123!")).Succeeded);
    }

    [Fact]
    public async Task ServiceEdit_PreservesAppointmentSnapshotAndOriginalCreatedAt()
    {
        await using var f = new Fixture();
        var created = new DateTime(2025, 1, 1);
        var service = new Service { ServiceName = "Cắt", Price = 100000, DurationMinutes = 30, CreatedAt = created };
        var appointment = new Appointment { StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(9.5) };
        var snapshot = new AppointmentService { Service = service, Appointment = appointment, DurationMinutes = 30, Price = 100000 };
        f.Db.Add(snapshot); await f.Db.SaveChangesAsync();
        var controller = new ServicesController(f.Db) { TempData = new TempDataDictionary(f.Http, new TempProvider()) };
        Assert.IsType<RedirectToActionResult>(await controller.Edit(service.ServiceId,
            new Service { ServiceId = service.ServiceId, ServiceName = "Cắt", Price = 150000, DurationMinutes = 60 }));
        Assert.Equal(30, snapshot.DurationMinutes);
        Assert.Equal(100000, snapshot.Price);
        Assert.Equal(TimeSpan.FromHours(9.5), appointment.EndTime);
        Assert.Equal(created, service.CreatedAt);
        Assert.IsType<RedirectToActionResult>(await controller.Delete(service.ServiceId));
        Assert.Single(f.Db.Services);
        Assert.NotNull(controller.TempData["Error"]);
    }

    [Fact]
    public async Task ServicePrice_RejectsFractionalVnd()
    {
        await using var f = new Fixture();
        var controller = new ServicesController(f.Db);
        Assert.IsType<ViewResult>(await controller.Create(new Service { ServiceName = "Cắt", Price = 100.5m, DurationMinutes = 30 }));
        Assert.Contains("Price", controller.ModelState.Keys);
        Assert.Empty(f.Db.Services);
    }

    [Fact]
    public async Task Audit_CapturesActorAndCrudAndRejectsMutationWithoutLeakingCredentials()
    {
        await using var f = new Fixture();
        var user = await f.CreateUser(UserRoles.Stylist);
        var service = new Service { ServiceName = "Cắt" };
        f.Db.Add(service); await f.Db.SaveChangesAsync();
        service.ServiceName = "Gội"; await f.Db.SaveChangesAsync();
        f.Db.Remove(service); await f.Db.SaveChangesAsync();
        var logs = await f.Db.AuditLogs.Where(l => l.EntityType == nameof(Service)).ToListAsync();
        Assert.Equal(new[] { "CREATE", "UPDATE", "DELETE" }, logs.Select(l => l.Action));
        Assert.All(logs, l => { Assert.Equal("actor", l.UserId); Assert.Equal("actor@salon.local", l.UserName); Assert.Equal(service.ServiceId.ToString(), l.EntityId); });
        var userLogs = await f.Db.AuditLogs.Where(l => l.EntityType == nameof(ApplicationUser)).ToListAsync();
        Assert.All(userLogs, l => { Assert.DoesNotContain("PasswordHash", l.Changes); Assert.DoesNotContain("SecurityStamp", l.Changes); });
        logs[0].Action = "TAMPER";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync(true));
        f.Db.ChangeTracker.Clear();
        f.Db.Remove(await f.Db.AuditLogs.FirstAsync());
        Assert.Throws<InvalidOperationException>(() => f.Db.SaveChanges(true));
    }

    private static ClaimsPrincipal Principal(ApplicationUser user, string role) => new(new ClaimsIdentity(
        [new("sub", user.Id), new("security_stamp", user.SecurityStamp!), new(ClaimTypes.Role, role)], "test"));

    private sealed class CapturingEmail : IEmailService
    {
        public string? Password { get; private set; }
        public bool Fail { get; init; }
        public Task SendTemporaryPasswordEmailAsync(string email, string password) { if (Fail) throw new InvalidOperationException("Test delivery failure"); Password = password; return Task.CompletedTask; }
        public Task SendPasswordResetEmailAsync(string email, string link) => Task.CompletedTask;
        public Task SendEmailVerificationCodeAsync(string email, string code) => Task.CompletedTask;
    }
    private sealed class TempProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly AsyncServiceScope scope;
        public ApplicationDbContext Db { get; }
        public UserManager<ApplicationUser> Users { get; }
        public RoleManager<IdentityRole> Roles { get; }
        public DefaultHttpContext Http { get; } = new() { User = new ClaimsPrincipal(new ClaimsIdentity([new("sub", "actor"), new("email", "actor@salon.local")], "test")) };
        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddLogging(); services.AddDataProtection();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = Http });
            var name = Guid.NewGuid().ToString();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(name));
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
            services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));
            provider = services.BuildServiceProvider(); scope = provider.CreateAsyncScope();
            Db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        }
        public async Task<ApplicationUser> CreateUser(string role)
        {
            await RoleSeeder.SeedRolesAsync(Roles);
            var user = new ApplicationUser { UserName = "user@salon.local", Email = "user@salon.local", EmailConfirmed = true };
            Assert.True((await Users.CreateAsync(user, "Password123!")).Succeeded);
            Assert.True((await Users.AddToRoleAsync(user, role)).Succeeded);
            return user;
        }
        public async ValueTask DisposeAsync() { await scope.DisposeAsync(); await provider.DisposeAsync(); }
    }
}
