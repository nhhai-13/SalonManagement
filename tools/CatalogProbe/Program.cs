using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;

// Isolated measurement host: actual MVC/Razor + static assets, synthetic data only.
var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../SalonManagement"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(ServicesController).Assembly.GetName().Name,
    ContentRootPath = contentRoot,
    WebRootPath = Path.Combine(contentRoot, "wwwroot"),
    EnvironmentName = "Production"
});
builder.WebHost.UseUrls("http://127.0.0.1:5183");
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase("catalog-probe"));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ServicesController).Assembly);
builder.Services.AddRazorPages();
var app = builder.Build();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Services}/{action=Public}/{id?}");
app.MapRazorPages();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var count = int.Parse(Environment.GetEnvironmentVariable("CATALOG_PROBE_SERVICES") ?? "100");
    var groupCount = int.Parse(Environment.GetEnvironmentVariable("CATALOG_PROBE_GROUPS") ?? "10");
    if (count < 1 || groupCount < 1 || groupCount > count) throw new ArgumentException("Invalid sample size.");
    var stylist = new Stylist { FullName = "Thợ mẫu" };
    var groups = Enumerable.Range(1, groupCount).Select(i => new ServiceGroup
    { GroupName = i == 1 ? "Nhóm " + new string('Đ', 100) : $"Nhóm {i}", DisplayOrder = i }).ToArray();
    for (var i = 0; i < count; i++)
        groups[i % groupCount].Services.Add(new Service
        {
            ServiceName = i == 0 ? "Cắt tóc " + new string('ắ', 200) : $"Gội đầu thư giãn {i:D4}",
            Description = "Chăm sóc tóc và da đầu chuyên sâu, tư vấn kiểu tóc phù hợp.",
            DurationMinutes = 240, Price = 20000000,
            Stylists = [new() { Stylist = stylist }]
        });
    db.AddRange(groups);
    await db.SaveChangesAsync();
}
await app.RunAsync();
