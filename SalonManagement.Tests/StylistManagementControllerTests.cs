using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task Create_SavesProfileAndMultipleUniqueServiceAssignments()
    {
        using var fixture = new Fixture();
        var first = fixture.AddService("Cắt tóc");
        var second = fixture.AddService("Gội đầu");
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Create(new CreateStylistViewModel
        {
            FullName = "  Nguyễn Văn A  ",
            Phone = "0987654321",
            Description = "Thợ có 3 năm kinh nghiệm",
            ProfileImage = Image("avatar.jpg", "image/jpeg", ValidJpeg(1536 * 1024)),
            ServiceIds = [first.ServiceId, first.ServiceId, second.ServiceId]
        });

        Assert.IsType<RedirectToActionResult>(result);
        var stylist = await fixture.Db.Stylists.Include(item => item.Services).SingleAsync();
        Assert.Equal("Nguyễn Văn A", stylist.FullName);
        Assert.True(stylist.IsActive);
        Assert.Equal("Thợ có 3 năm kinh nghiệm", stylist.Description);
        Assert.StartsWith("uploads/stylists/", stylist.ProfileImagePath);
        Assert.Equal(2, stylist.Services.Count);
        Assert.Equal(2, await fixture.Db.StylistServices.CountAsync());
    }

    [Theory]
    [InlineData("avatar.gif", "image/gif", new byte[] { 0x47, 0x49, 0x46 })]
    [InlineData("avatar.pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 })]
    [InlineData("avatar.webp", "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 })]
    [InlineData("renamed.jpg", "image/jpeg", new byte[] { 0x47, 0x49, 0x46 })]
    public async Task Create_RejectsUnsupportedImageFormats(string fileName, string contentType, byte[] content)
    {
        using var fixture = new Fixture();
        await fixture.AddServiceAndSaveAsync();

        var result = await fixture.Create(fixture.ValidRequest(Image(fileName, contentType, content)));

        Assert.IsType<ViewResult>(result);
        Assert.Contains(fixture.Controller.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage == "Ảnh đại diện chỉ được phép ở định dạng JPG hoặc PNG.");
        Assert.Empty(await fixture.Db.Stylists.ToListAsync());
        if (Directory.Exists(fixture.WebRootPath))
            Assert.Empty(Directory.GetFiles(fixture.WebRootPath, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Create_RejectsImagesLargerThanTwoMegabytes()
    {
        using var fixture = new Fixture();
        await fixture.AddServiceAndSaveAsync();
        var content = new byte[2 * 1024 * 1024 + 1];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;

        var result = await fixture.Create(fixture.ValidRequest(Image("avatar.jpg", "image/jpeg", content)));

        Assert.IsType<ViewResult>(result);
        Assert.Contains(fixture.Controller.ModelState.Values.SelectMany(value => value.Errors),
            error => error.ErrorMessage == "Ảnh đại diện không được vượt quá 2MB.");
        Assert.Empty(await fixture.Db.Stylists.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsMissingRequiredFieldsAndUnknownServices()
    {
        using var fixture = new Fixture();
        await fixture.AddServiceAndSaveAsync();
        var validImage = Image("avatar.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var noServices = fixture.ValidRequest(validImage);
        noServices.ServiceIds = [];
        Assert.IsType<ViewResult>(await fixture.Create(noServices));
        Assert.Contains(fixture.Controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage == "Vui lòng chọn ít nhất một dịch vụ cho thợ.");

        fixture.Controller.ModelState.Clear();
        var unknownService = fixture.ValidRequest(validImage);
        unknownService.ServiceIds = [int.MaxValue];
        Assert.IsType<ViewResult>(await fixture.Create(unknownService));
        Assert.Contains(fixture.Controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage == "Một hoặc nhiều dịch vụ không tồn tại hoặc đã ngừng cung cấp.");

        fixture.Controller.ModelState.Clear();
        var missingImage = fixture.ValidRequest(null);
        Assert.IsType<ViewResult>(await fixture.Create(missingImage));
        Assert.Contains(fixture.Controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage == "Vui lòng chọn ảnh đại diện.");

        fixture.Controller.ModelState.Clear();
        var blankName = fixture.ValidRequest(validImage);
        blankName.FullName = "   ";
        Assert.IsType<ViewResult>(await fixture.Create(blankName));
        Assert.Contains(fixture.Controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage == "Vui lòng nhập họ tên.");

        fixture.Controller.ModelState.Clear();
        var missingPhone = fixture.ValidRequest(validImage);
        missingPhone.Phone = "   ";
        Assert.IsType<ViewResult>(await fixture.Create(missingPhone));
        Assert.Contains(fixture.Controller.ModelState.Values.SelectMany(value => value.Errors), error => error.ErrorMessage == "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.");
        Assert.Empty(await fixture.Db.Stylists.ToListAsync());
    }

    [Fact]
    public async Task Create_AllowsMultipleStylistsToShareAService_AndAcceptsPng()
    {
        using var fixture = new Fixture();
        var sharedService = fixture.AddService("Tạo kiểu");
        await fixture.Db.SaveChangesAsync();
        var png = () => Image("avatar.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Assert.IsType<RedirectToActionResult>(await fixture.Create(fixture.ValidRequest(png(), "Nguyễn Văn A")));
        fixture.Controller.ModelState.Clear();
        Assert.IsType<RedirectToActionResult>(await fixture.Create(fixture.ValidRequest(png(), "Trần Văn B")));

        Assert.Equal(2, await fixture.Db.Stylists.CountAsync());
        Assert.Equal(2, await fixture.Db.StylistServices.CountAsync(item => item.ServiceId == sharedService.ServiceId));
    }

    private static FormFile Image(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, content.Length, "ProfileImage", fileName)
        {
            Headers = new HeaderDictionary()
        };
        file.ContentType = contentType;
        return file;
    }

    private static byte[] ValidJpeg(int length)
    {
        var bytes = new byte[length];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "stylist-tests-" + Guid.NewGuid().ToString("N"));

        public Fixture()
        {
            Directory.CreateDirectory(_root);
            WebRootPath = Path.Combine(_root, "wwwroot");
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            Db = new ApplicationDbContext(options, new HttpContextAccessor());
            Controller = new StylistManagementController(Db, new TestEnvironment(WebRootPath), NullLogger<StylistManagementController>.Instance);
            Controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new EmptyTempDataProvider());
        }

        public ApplicationDbContext Db { get; }
        public StylistManagementController Controller { get; }
        public string WebRootPath { get; }

        public Service AddService(string name)
        {
            var service = new Service { ServiceName = name, DurationMinutes = 30, Price = 100, IsActive = true };
            Db.Services.Add(service);
            return service;
        }

        public async Task AddServiceAndSaveAsync()
        {
            AddService("Cắt tóc");
            await Db.SaveChangesAsync();
        }

        public CreateStylistViewModel ValidRequest(IFormFile? image, string name = "Nguyễn Văn A") => new()
        {
            FullName = name,
            Phone = "0987654321",
            ProfileImage = image,
            ServiceIds = Db.Services.Select(service => service.ServiceId).ToList()
        };

        public async Task<IActionResult> Create(CreateStylistViewModel model)
        {
            Controller.ModelState.Clear();
            return await Controller.Create(model);
        }

        public void Dispose()
        {
            Db.Dispose();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SalonManagement.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = webRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
