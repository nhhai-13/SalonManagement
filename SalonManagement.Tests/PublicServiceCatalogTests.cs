using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using Xunit;

namespace SalonManagement.Tests;

public class PublicServiceCatalogTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AnonymousRequest_RendersCatalogAndEmptyStates(int groupCount)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ServicesController).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var database = Guid.NewGuid().ToString();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build());
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(ServicesController).Assembly);
        builder.Services.AddRazorPages();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (groupCount >= 0)
                db.ServiceGroups.Add(new() { GroupName = "Nhóm rỗng", DisplayOrder = 9 });
            if (groupCount > 0)
            {
                db.ServiceGroups.Add(new() { GroupName = "Tóc", DisplayOrder = 2, Services = [
                    new() { ServiceName = "Z - Uốn tóc", DurationMinutes = 90, Price = 1250000, IsActive = false },
                    new() { ServiceName = "A - Cắt tóc", DurationMinutes = 30, Price = 150000 }
                ] });
            }
            if (groupCount > 1)
            {
                db.ServiceGroups.Add(new() { GroupName = "Gội", DisplayOrder = 1, Services = [
                    new() { ServiceName = "Gội thư giãn", DurationMinutes = 45, Price = 250000 }
                ] });
                db.Services.Add(new() { ServiceName = "Dịch vụ lẻ", DurationMinutes = 15, Price = 50000 });
            }
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri(address) };
            foreach (var path in new[] { "/Services", "/Services/Public" })
            {
                var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                if (groupCount >= 0)
                    Assert.Contains("Nhóm này chưa có dịch vụ.", html);
                else
                    Assert.DoesNotContain("aria-label=\"Nhóm dịch vụ\"", html);
                if (groupCount <= 0)
                    Assert.Contains("Chưa có dịch vụ nào để hiển thị.", html);
                else
                {
                    Assert.DoesNotContain("Chưa có dịch vụ nào để hiển thị.", html);
                    Assert.Contains("A - Cắt tóc</h3>", html);
                    Assert.Contains("Z - Uốn tóc</h3>", html);
                    Assert.Contains("Thời lượng: 30 phút", html);
                    Assert.Contains("Thời lượng: 90 phút", html);
                    Assert.Contains("Giá: 150.000 VND", html);
                    Assert.Contains("Giá: 1.250.000 VND", html);
                    Assert.True(html.IndexOf("A - Cắt tóc</h3>") < html.IndexOf("Z - Uốn tóc</h3>"));
                    if (groupCount > 1)
                    {
                        Assert.True(html.IndexOf("Gội</h2>") < html.IndexOf("Tóc</h2>"));
                        Assert.True(html.IndexOf("Tóc</h2>") < html.IndexOf("Chưa phân nhóm</h2>"));
                        Assert.Contains("Gội thư giãn</h3>", html);
                        Assert.Contains("Dịch vụ lẻ</h3>", html);
                    }
                }
            }
        }
        finally { await app.StopAsync(); }
    }
}
