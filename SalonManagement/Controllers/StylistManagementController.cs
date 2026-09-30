using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
[Route("management/stylists")]
public sealed class StylistManagementController(
    ApplicationDbContext db,
    IWebHostEnvironment environment,
    ILogger<StylistManagementController> logger) : Controller
{
    private const long MaxProfileImageBytes = 2 * 1024 * 1024;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var stylists = await db.Stylists.AsNoTracking()
            .Include(stylist => stylist.Services)
            .ThenInclude(item => item.Service)
            .OrderBy(stylist => stylist.FullName)
            .ToListAsync();

        return View(stylists);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var model = new CreateStylistViewModel();
        await LoadServicesAsync(model);
        return View(model);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStylistViewModel model)
    {
        model.FullName = model.FullName?.Trim() ?? string.Empty;
        model.Phone = model.Phone?.Trim() ?? string.Empty;
        model.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

        if (!Regex.IsMatch(model.Phone, @"^0\d{9}$"))
            ModelState.AddModelError(nameof(model.Phone), "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.");
        else if (await db.Stylists.AnyAsync(stylist => stylist.Phone == model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "Số điện thoại này đã thuộc một hồ sơ thợ khác.");

        if (model.ProfileImage is null || model.ProfileImage.Length == 0)
            ModelState.AddModelError(nameof(model.ProfileImage), "Vui lòng chọn ảnh đại diện.");

        var serviceIds = model.ServiceIds.Distinct().ToArray();
        if (serviceIds.Length == 0)
            ModelState.AddModelError(nameof(model.ServiceIds), "Vui lòng chọn ít nhất một dịch vụ cho thợ.");

        var services = await db.Services
            .Where(service => service.IsActive && serviceIds.Contains(service.ServiceId))
            .ToListAsync();
        if (services.Count != serviceIds.Length)
            ModelState.AddModelError(nameof(model.ServiceIds), "Một hoặc nhiều dịch vụ không tồn tại hoặc đã ngừng cung cấp.");

        var imageError = model.ProfileImage is null ? null : await ValidateProfileImageAsync(model.ProfileImage);
        if (imageError is not null)
            ModelState.AddModelError(nameof(model.ProfileImage), imageError);

        if (!ModelState.IsValid)
        {
            await LoadServicesAsync(model);
            return View(model);
        }

        var image = model.ProfileImage!;
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = $"uploads/stylists/{fileName}";
        var uploadDirectory = Path.Combine(environment.WebRootPath, "uploads", "stylists");
        var absolutePath = Path.Combine(uploadDirectory, fileName);
        Directory.CreateDirectory(uploadDirectory);

        try
        {
            await using (var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await image.CopyToAsync(fileStream);

            var stylist = new Stylist
            {
                FullName = model.FullName,
                Phone = model.Phone,
                Description = model.Description,
                ProfileImagePath = relativePath,
                Services = services.Select(service => new StylistService { ServiceId = service.ServiceId }).ToList()
            };

            db.Stylists.Add(stylist);
            await db.SaveChangesAsync();
            TempData["Success"] = $"Đã tạo hồ sơ thợ {stylist.FullName}.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception exception)
        {
            if (System.IO.File.Exists(absolutePath))
                System.IO.File.Delete(absolutePath);

            logger.LogError(exception, "Could not create stylist profile.");
            ModelState.AddModelError(string.Empty, "Không thể lưu hồ sơ thợ. Vui lòng thử lại.");
            await LoadServicesAsync(model);
            return View(model);
        }
    }

    [HttpPost("{id:int}/resign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resign(int id)
    {
        var stylist = await db.Stylists.SingleOrDefaultAsync(item => item.StylistId == id);
        if (stylist is null)
            return NotFound();

        if (stylist.IsActive)
        {
            stylist.IsActive = false;
            stylist.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();
            TempData["Success"] = $"Đã chuyển {stylist.FullName} sang trạng thái nghỉ việc.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadServicesAsync(CreateStylistViewModel model)
    {
        model.AvailableServices = await db.Services.AsNoTracking()
            .Where(service => service.IsActive)
            .OrderBy(service => service.ServiceName)
            .ToListAsync();
    }

    private static async Task<string?> ValidateProfileImageAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mimeAllowed = extension switch
        {
            ".jpg" or ".jpeg" => file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase),
            ".png" => file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        if (!mimeAllowed)
            return "Ảnh đại diện chỉ được phép ở định dạng JPG hoặc PNG.";
        if (file.Length > MaxProfileImageBytes)
            return "Ảnh đại diện không được vượt quá 2MB.";

        var header = new byte[8];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header);
        var isJpeg = extension is ".jpg" or ".jpeg" && read >= 3 &&
                     header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = extension == ".png" && read == 8 &&
                    header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        return isJpeg || isPng ? null : "Nội dung tệp không phải ảnh JPG hoặc PNG hợp lệ.";
    }
}
