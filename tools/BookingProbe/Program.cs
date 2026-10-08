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
builder.WebHost.UseUrls("http://127.0.0.1:5184");
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase("booking-probe"));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<StylistAvailabilityService>();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ServicesController).Assembly);
builder.Services.AddRazorPages();
var app = builder.Build();
app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); app.MapRazorPages();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var now = scope.ServiceProvider.GetRequiredService<StylistAvailabilityService>().SalonNow;
    await StylistBookingDemoSeed.SeedAsync(db, now.Date.AddDays(1));
    // Extra active service without a matching stylist exercises the empty state.
    db.Services.Add(new() { ServiceName = "Demo Không có thợ", DurationMinutes = 30, Price = 50000 });
    await db.SaveChangesAsync();
}
await app.RunAsync();
