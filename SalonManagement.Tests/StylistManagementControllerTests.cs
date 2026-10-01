using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SalonManagement.Controllers;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using Xunit;

namespace SalonManagement.Tests;

public sealed class StylistManagementControllerTests
{
    [Fact]
    public void Controller_AllowsOnlyOwnerRole()
    {
        var authorize = Assert.Single(typeof(StylistManagementController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal(UserRoles.Owner, authorize.Roles);
    }

    [Fact]
    public async Task Create_ValidProfile_PersistsStylistAndAssignedService()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), $"salon-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(webRoot);

        try
        {
            await using var db = CreateDb();
            var service = new Service { ServiceName = "Cắt tóc", Price = 100_000, DurationMinutes = 45 };
            db.Services.Add(service);
            await db.SaveChangesAsync();

            var controller = CreateController(db, webRoot);
            var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
            var model = new CreateStylistViewModel
            {
                FullName = "Nguyễn Minh Anh",
                Phone = "0912345678",
                Description = "Chuyên gia tạo kiểu",
                ServiceIds = [service.ServiceId],
                ProfileImage = new FormFile(new MemoryStream(imageBytes), 0, imageBytes.Length, "ProfileImage", "avatar.png")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "image/png"
                }
            };

            var result = await controller.Create(model);

            Assert.IsType<RedirectToActionResult>(result);
            var stylist = await db.Stylists.Include(item => item.Services).SingleAsync();
            Assert.Equal("Nguyễn Minh Anh", stylist.FullName);
            Assert.Equal(service.ServiceId, Assert.Single(stylist.Services).ServiceId);
            Assert.True(File.Exists(Path.Combine(webRoot, stylist.ProfileImagePath!.Replace('/', Path.DirectorySeparatorChar))));
        }
        finally
        {
            if (Directory.Exists(webRoot))
                Directory.Delete(webRoot, true);
        }
    }

    [Fact]
    public async Task Resign_ActiveStylist_MarksProfileInactive()
    {
        await using var db = CreateDb();
        var stylist = new Stylist { FullName = "Trần Thu Hà", Phone = "0987654321" };
        db.Stylists.Add(stylist);
        await db.SaveChangesAsync();
        var controller = CreateController(db, Path.GetTempPath());

        var result = await controller.Resign(stylist.StylistId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.False(stylist.IsActive);
        Assert.NotNull(stylist.UpdatedAt);
    }

    [Fact]
    public async Task Reinstate_InactiveStylist_MarksProfileActive()
    {
        await using var db = CreateDb();
        var stylist = new Stylist
        {
            FullName = "Đỗ Quốc Huy",
            Phone = "0901000005",
            IsActive = false
        };
        db.Stylists.Add(stylist);
        await db.SaveChangesAsync();
        var controller = CreateController(db, Path.GetTempPath());

        var result = await controller.Reinstate(stylist.StylistId);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.True(stylist.IsActive);
        Assert.NotNull(stylist.UpdatedAt);
    }

    [Fact]
    public async Task Edit_ValidProfile_UpdatesDetailsAndKeepsExistingImage()
    {
        await using var db = CreateDb();
        var oldService = new Service { ServiceName = "Cắt tóc", Price = 100_000, DurationMinutes = 30 };
        var newService = new Service { ServiceName = "Tạo kiểu", Price = 200_000, DurationMinutes = 60 };
        db.Services.AddRange(oldService, newService);
        await db.SaveChangesAsync();
        var stylist = new Stylist
        {
            FullName = "Tên cũ",
            Phone = "0901000001",
            ProfileImagePath = "uploads/stylists/existing.png",
            Services = [new StylistService { ServiceId = oldService.ServiceId }]
        };
        db.Stylists.Add(stylist);
        await db.SaveChangesAsync();
        var controller = CreateController(db, Path.GetTempPath());
        var model = new EditStylistViewModel
        {
            StylistId = stylist.StylistId,
            FullName = "Tên mới",
            Phone = "0901000099",
            Description = "Chuyên gia tạo kiểu",
            ServiceIds = [newService.ServiceId]
        };

        var result = await controller.Edit(stylist.StylistId, model);

        Assert.IsType<RedirectToActionResult>(result);
        var updated = await db.Stylists.Include(item => item.Services)
            .SingleAsync(item => item.StylistId == stylist.StylistId);
        Assert.Equal("Tên mới", updated.FullName);
        Assert.Equal("0901000099", updated.Phone);
        Assert.Equal("Chuyên gia tạo kiểu", updated.Description);
        Assert.Equal("uploads/stylists/existing.png", updated.ProfileImagePath);
        Assert.Equal(newService.ServiceId, Assert.Single(updated.Services).ServiceId);
        Assert.NotNull(updated.UpdatedAt);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private static StylistManagementController CreateController(ApplicationDbContext db, string webRoot) =>
        new(db, new TestEnvironment(webRoot), NullLogger<StylistManagementController>.Instance)
        {
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new DefaultHttpContext(),
                new TestTempDataProvider())
        };

    private sealed class TestEnvironment(string webRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SalonManagement.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = webRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
