using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SalonManagement"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{ ApplicationName = typeof(ServicesController).Assembly.GetName().Name, ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
builder.Logging.ClearProviders(); builder.Logging.AddConsole();
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("BOOKING_PROBE_URL") ?? "http://127.0.0.1:5184");
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
var sqlTest = Environment.GetEnvironmentVariable("BOOKING_PROBE_SQL") == "1";
builder.Services.AddDbContext<ApplicationDbContext>(o => {
    if (sqlTest) o.UseSqlServer("Server=localhost;Database=SalonManagement_S206_Test_20261004;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=5");
    else o.UseInMemoryDatabase("booking-probe");
});
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<StylistAvailabilityService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<AppointmentBookingService>();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ServicesController).Assembly);
builder.Services.AddRazorPages();
var app = builder.Build();
app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); app.MapRazorPages();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (sqlTest) await db.Database.MigrateAsync();
    var now = scope.ServiceProvider.GetRequiredService<StylistAvailabilityService>().SalonNow;
    await StylistBookingDemoSeed.SeedAsync(db, now.Date.AddDays(1));
    await StylistAssignmentDemoSeed.SeedAsync(db, now.Date.AddDays(1));
    // Extra active service without a matching stylist exercises the empty state.
    db.Services.Add(new() { ServiceName = "Demo Không có thợ", DurationMinutes = 30, Price = 50000 });
    await db.SaveChangesAsync();
}
await app.RunAsync();
