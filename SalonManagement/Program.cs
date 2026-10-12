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
    if (builder.Configuration["DatabaseProvider"] == "Sqlite")
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// =====================================
// IDENTITY
// =====================================

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

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

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(
            new DirectoryInfo(
                Path.Combine(
                    builder.Environment.ContentRootPath,
                    ".keys")))
        .SetApplicationName(
            "SalonManagement.Development");
}
// =====================================
// JWT AUTHENTICATION
// =====================================

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
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
        OnTokenValidated = async context =>
        {
            var userId =
                context.Principal?.FindFirstValue(
                    JwtRegisteredClaimNames.Sub);

            var userManager =
                context.HttpContext.RequestServices
                    .GetRequiredService<
                        UserManager<ApplicationUser>>();

            var user = userId is null
                ? null
                : await userManager.FindByIdAsync(userId);

            if (user is null || !user.IsActive)
            {
                context.Fail("Account is inactive.");
            }
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

builder.Services.AddScoped<
    ITokenService,
    TokenService>();

builder.Services.AddScoped<CustomerProfileService>();
builder.Services.AddScoped<CustomerProfileManagementService>();
builder.Services.AddScoped<BookingService>();

builder.Services.AddMemoryCache();

builder.Services.AddScoped<
    IEmailService,
    EmailService>();

builder.Services.Configure<ScheduleEmailOptions>(
    builder.Configuration.GetSection("ScheduleChangeEmail:Smtp"));
builder.Services.Configure<ScheduleNotificationOptions>(
    builder.Configuration.GetSection("ScheduleChangeEmail:Delivery"));
builder.Services.AddSingleton<IScheduleChangeEmailSender, SmtpScheduleChangeEmailSender>();
builder.Services.AddScoped<ScheduleNotificationDispatcher>();
builder.Services.AddHostedService<ScheduleNotificationBackgroundService>();

builder.Services.AddSingleton<
    IPasswordResetRateLimiter,
    PasswordResetRateLimiter>();

builder.Services.AddControllersWithViews();

// =====================================
// BUILD APPLICATION
// =====================================

var app = builder.Build();

// =====================================
// SQLITE DATABASE INITIALIZATION
// =====================================

if (builder.Configuration["DatabaseProvider"] == "Sqlite")
{
    await using var scope =
        app.Services.CreateAsyncScope();

    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
    await EnsureSqliteColumnAsync(dbContext, "Customers", "NormalizedPhone", "TEXT NOT NULL DEFAULT ''");
    await dbContext.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Customers_NormalizedPhone\" ON \"Customers\" (\"NormalizedPhone\")");
    await dbContext.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "CustomerProfileChangeLogs" (
            "CustomerProfileChangeLogId" INTEGER NOT NULL CONSTRAINT "PK_CustomerProfileChangeLogs" PRIMARY KEY AUTOINCREMENT,
            "CustomerId" INTEGER NOT NULL,
            "ChangedByUserId" TEXT NOT NULL,
            "ChangedByEmail" TEXT NOT NULL,
            "PreviousFullName" TEXT NOT NULL,
            "PreviousEmail" TEXT NULL,
            "PreviousNotes" TEXT NULL,
            "UpdatedFullName" TEXT NOT NULL,
            "UpdatedEmail" TEXT NULL,
            "UpdatedNotes" TEXT NULL,
            "ChangedAtUtc" TEXT NOT NULL,
            CONSTRAINT "FK_CustomerProfileChangeLogs_Customers_CustomerId"
                FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("CustomerId") ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS "IX_CustomerProfileChangeLogs_CustomerId_ChangedAtUtc"
            ON "CustomerProfileChangeLogs" ("CustomerId", "ChangedAtUtc");
        """);
    await dbContext.Database.ExecuteSqlRawAsync("""
        UPDATE Customers SET NormalizedPhone =
            CASE WHEN substr(PhoneDigits, 1, 2) = '84' AND length(PhoneDigits) >= 11
                THEN '0' || substr(PhoneDigits, 3)
                ELSE PhoneDigits END
        FROM (
            SELECT CustomerId,
                replace(replace(replace(replace(replace(replace(replace(replace(Phone, '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), '.', ''), '/', ''), char(9), '') AS PhoneDigits
            FROM Customers WHERE NormalizedPhone = ''
        ) AS digits
        WHERE Customers.CustomerId = digits.CustomerId
        """);
    await dbContext.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "ScheduleContactFollowUps" (
            "ScheduleContactFollowUpId" INTEGER NOT NULL CONSTRAINT "PK_ScheduleContactFollowUps" PRIMARY KEY AUTOINCREMENT,
            "AppointmentId" INTEGER NOT NULL,
            "Category" TEXT NOT NULL,
            "Reason" TEXT NOT NULL,
            "OriginalAppointmentDate" TEXT NOT NULL,
            "OriginalStartTime" TEXT NOT NULL,
            "ContactStatus" TEXT NOT NULL,
            "ContactNote" TEXT NULL,
            "ContactedAt" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL,
            CONSTRAINT "FK_ScheduleContactFollowUps_Appointments_AppointmentId"
                FOREIGN KEY ("AppointmentId") REFERENCES "Appointments" ("AppointmentId") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ScheduleContactFollowUps_AppointmentId"
            ON "ScheduleContactFollowUps" ("AppointmentId");
        CREATE INDEX IF NOT EXISTS "IX_ScheduleContactFollowUps_OriginalAppointmentDate_ContactStatus_Category"
            ON "ScheduleContactFollowUps" ("OriginalAppointmentDate", "ContactStatus", "Category");

        CREATE TABLE IF NOT EXISTS "ScheduleChangeNotifications" (
            "ScheduleChangeNotificationId" INTEGER NOT NULL CONSTRAINT "PK_ScheduleChangeNotifications" PRIMARY KEY AUTOINCREMENT,
            "AppointmentId" INTEGER NOT NULL,
            "Version" INTEGER NOT NULL,
            "RecipientEmail" TEXT NOT NULL,
            "CustomerName" TEXT NOT NULL,
            "ServiceNames" TEXT NOT NULL,
            "AppointmentDate" TEXT NOT NULL,
            "OldStylistName" TEXT NOT NULL,
            "NewStylistName" TEXT NOT NULL,
            "OldStartTime" TEXT NOT NULL,
            "NewStartTime" TEXT NOT NULL,
            "TransferredAt" TEXT NOT NULL,
            "Status" TEXT NOT NULL,
            "AttemptCount" INTEGER NOT NULL,
            "LastAttemptAt" TEXT NULL,
            "NextAttemptAt" TEXT NULL,
            "SentAt" TEXT NULL,
            "LastError" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            CONSTRAINT "FK_ScheduleChangeNotifications_Appointments_AppointmentId"
                FOREIGN KEY ("AppointmentId") REFERENCES "Appointments" ("AppointmentId") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ScheduleChangeNotifications_AppointmentId_Version"
            ON "ScheduleChangeNotifications" ("AppointmentId", "Version");
        CREATE INDEX IF NOT EXISTS "IX_ScheduleChangeNotifications_Status_NextAttemptAt"
            ON "ScheduleChangeNotifications" ("Status", "NextAttemptAt");
        """);
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

    if (app.Environment.IsDevelopment())
    {
        var salonTimeZone = TimeZoneInfo.FindSystemTimeZoneById(BusinessHour.SalonTimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, salonTimeZone));
        await DevelopmentBookingScheduleSeeder.SeedAsync(dbContext, today);
    }
}

// =====================================
// RUN
// =====================================

app.Run();

static async Task EnsureSqliteColumnAsync(
    ApplicationDbContext dbContext,
    string table,
    string column,
    string definition)
{
    if (table != "Customers" || column != "NormalizedPhone" || definition != "TEXT NOT NULL DEFAULT ''")
        throw new ArgumentOutOfRangeException(nameof(column), "Unexpected SQLite schema alteration.");

    var connection = dbContext.Database.GetDbConnection();
    await using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info(\"{table}\")";
    if (connection.State != System.Data.ConnectionState.Open)
        await connection.OpenAsync();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            return;
    }
    await reader.DisposeAsync();
    await using var alterCommand = connection.CreateCommand();
    alterCommand.CommandText = "ALTER TABLE \"Customers\" ADD COLUMN \"NormalizedPhone\" TEXT NOT NULL DEFAULT ''";
    await alterCommand.ExecuteNonQueryAsync();
}
