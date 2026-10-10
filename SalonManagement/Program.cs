using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
// =====================================
// LOGGING
// =====================================

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// =====================================
// DATABASE
// =====================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServerOptions =>
        sqlServerOptions.EnableRetryOnFailure());
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// =====================================
// IDENTITY
// =====================================

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;

    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan =
        TimeSpan.FromMinutes(15);
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// =====================================
// PASSWORD RESET TOKEN
// =====================================

builder.Services.Configure<DataProtectionTokenProviderOptions>(
    options =>
    {
        options.TokenLifespan = TimeSpan.FromMinutes(30);
    });

// =====================================
// DATA PROTECTION
// =====================================

if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(
            new DirectoryInfo(
                Path.Combine(
                    builder.Environment.ContentRootPath,
                    ".keys")))
        .SetApplicationName(
            $"SalonManagement.{builder.Environment.EnvironmentName}");
}

// =====================================
// JWT AUTHENTICATION
// =====================================

builder.Services.AddAuthentication(options =>
{
    // The application issues JWTs (and mirrors the access token into an
    // HttpOnly cookie for page navigations). AddDefaultIdentity registers its
    // application cookie as the default scheme, so explicitly select bearer
    // authentication for every protected MVC/API endpoint.
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwt = builder.Configuration.GetSection("Jwt");

    options.MapInboundClaims = false;
    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt["Key"]!)),

            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };
    options.Events = new JwtBearerEvents
    {
        // Browser page navigations cannot attach an Authorization header.
        // Accept the same JWT from the secure HttpOnly cookie issued at login.
        OnMessageReceived = context =>
        {
            if (string.IsNullOrWhiteSpace(context.Token) &&
                !context.Request.Headers.ContainsKey("Authorization") &&
                context.Request.Cookies.TryGetValue("salon.accessToken", out var cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var validator = context.HttpContext.RequestServices.GetRequiredService<SessionPrincipalValidator>();
            if (context.Principal == null || !await validator.ValidateAsync(context.Principal))
                context.Fail("Session has been revoked or account is inactive.");
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = 401,
                message = "Bạn cần đăng nhập để thực hiện chức năng này."
            }));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = 403,
                message = "Bạn không có quyền thực hiện chức năng này."
            }));
        }
    };
});

// =====================================
// APPLICATION SERVICES
// =====================================

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAttendanceModules();
builder.Services.AddScoped<StylistAvailabilityService>();

builder.Services.AddScoped<
    ITokenService,
    TokenService>();

builder.Services.AddMemoryCache();

builder.Services.AddScoped<
    IEmailService,
    EmailService>();

builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();

builder.Services.AddScoped<IStaffAccountService, StaffAccountService>();

builder.Services.AddSingleton<
    IPasswordResetRateLimiter,
    PasswordResetRateLimiter>();

builder.Services.AddScoped<IBookingService, BookingService>();

builder.Services.AddScoped<SessionPrincipalValidator>();
builder.Services.AddControllersWithViews(options => options.Filters.Add<RequirePasswordChangeFilter>());

// =====================================
// BUILD APPLICATION
// =====================================

var app = builder.Build();

// =====================================
// DATABASE MIGRATION
// =====================================

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.MigrateAsync();
}

// =====================================
// HTTP REQUEST PIPELINE
// =====================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// =====================================
// REDIRECT DEFAULT IDENTITY PAGES
// =====================================

app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) &&
        (context.Request.Path.Equals(
             "/Identity/Account/Login",
             StringComparison.OrdinalIgnoreCase)
         ||
         context.Request.Path.Equals(
             "/Identity/Account/Register",
             StringComparison.OrdinalIgnoreCase)))
    {
        context.Response.Redirect("/admin/login");
        return;
    }

    await next();
});

// =====================================
// ROUTING + AUTHORIZATION
// =====================================

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// =====================================
// ROUTES
// =====================================

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// =====================================
// SEED ROLES + SAMPLE DATA
// =====================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // ApplicationDbContext
    var dbContext =
        services.GetRequiredService<
            ApplicationDbContext>();

    // RoleManager
    var roleManager =
        services.GetRequiredService<
            RoleManager<IdentityRole>>();

    // UserManager
    var userManager =
        services.GetRequiredService<
            UserManager<ApplicationUser>>();

    // ---------------------------------
    // 1. Tạo role trước
    // ---------------------------------

    await RoleSeeder.SeedRolesAsync(
        roleManager);

    // ---------------------------------
    // 2. Nạp dữ liệu mẫu
    //
    // - 3 nhóm dịch vụ
    // - 5 stylist
    // - 12 dịch vụ
    // - 4 tài khoản mẫu
    //
    // SeedData có kiểm tra dữ liệu tồn tại
    // nên có thể chạy nhiều lần.
    // ---------------------------------

    await SeedData.SeedAsync(
        dbContext,
        userManager);
    if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:StylistBookingDemo"))
    {
        var now = services.GetRequiredService<StylistAvailabilityService>().SalonNow;
        await StylistBookingDemoSeed.SeedAsync(dbContext, now.Date.AddDays(1));
    }
}

// =====================================
// RUN
// =====================================

app.Run();

