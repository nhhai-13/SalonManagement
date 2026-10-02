using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using System.Globalization;
using System.Text;

namespace SalonManagement.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Services
        // Cho phép xem danh sách dịch vụ
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? search = null)
        {
            var isOwner = User.IsInRole(UserRoles.Owner);
            if (!isOwner) return await Public(search);

            var services = await _context.Services.AsNoTracking()
                .Include(s => s.ServiceGroup)
                .Include(s => s.Stylists).ThenInclude(s => s.Stylist)
                .OrderBy(s => s.ServiceGroupId == null).ThenBy(s => s.ServiceGroup!.DisplayOrder).ThenBy(s => s.ServiceGroupId)
                .ThenBy(s => s.ServiceName)
                .ToListAsync();

            return View("Index", services);
        }

        // Owners can also preview the public catalog using /Services/Public.
        [AllowAnonymous]
        public async Task<IActionResult> Public(string? search = null)
        {
            search = search?.Trim() ?? string.Empty;
            var keyword = NormalizeSearch(search);
            var groups = await _context.ServiceGroups.AsNoTracking()
                .OrderBy(g => g.DisplayOrder).ThenBy(g => g.ServiceGroupId)
                .Select(g => new SalonManagement.Models.ViewModels.PublicServiceGroup
                {
                    Id = g.ServiceGroupId,
                    Name = g.GroupName
                }).ToListAsync();
            var services = await _context.Services.AsNoTracking()
                .Where(s => s.IsActive && s.Stylists.Any(link => link.Stylist.IsActive))
                .OrderBy(s => s.ServiceName).ThenBy(s => s.ServiceId)
                .Select(s => new Service
                {
                    ServiceId = s.ServiceId, ServiceGroupId = s.ServiceGroupId,
                    ServiceName = s.ServiceName, Description = s.Description,
                    DurationMinutes = s.DurationMinutes, Price = s.Price
                }).ToListAsync();
            var byGroup = services
                .Where(s => NormalizeSearch(s.ServiceName).Contains(keyword, StringComparison.Ordinal))
                .ToLookup(s => s.ServiceGroupId);
            foreach (var group in groups)
                group.Services = byGroup[group.Id].ToList();
            groups.RemoveAll(group => group.Services.Count == 0);
            if (byGroup[null].Any())
                groups.Add(new() { Name = "Ungrouped", Services = byGroup[null].ToList() });

            return View("Public", new SalonManagement.Models.ViewModels.PublicServiceCatalog { Groups = groups, Search = search });
        }

        // Normalize both sides in memory so Vietnamese matching is independent of database collation.
        private static string NormalizeSearch(string value)
        {
            var normalized = new StringBuilder();
            foreach (var character in value.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                    continue;
                var lower = char.ToLowerInvariant(character);
                normalized.Append(lower == 'đ' ? 'd' : lower);
            }
            return normalized.ToString();
        }

        // GET: /Services/Create
        [Authorize(Roles = UserRoles.Owner)]
        public IActionResult Create()
        {
            ViewBag.Groups = _context.ServiceGroups
                .OrderBy(g => g.DisplayOrder)
                .ToList();

            return View();
        }

        // POST: /Services/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Owner)]
        public async Task<IActionResult> Create([Bind("ServiceName,ServiceGroupId,DurationMinutes,Price,Description,IsActive")] Service service)
        {
            ValidateService(service);

            if (!ModelState.IsValid)
            {
                ViewBag.Groups = _context.ServiceGroups
                    .OrderBy(g => g.DisplayOrder)
                    .ToList();

                return View(service);
            }

            service.CreatedAt = DateTime.Now;

            _context.Services.Add(service);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã thêm dịch vụ mới";

            return RedirectToAction(nameof(Index));
        }

        // GET: /Services/Edit/5
        [Authorize(Roles = UserRoles.Owner)]
        public async Task<IActionResult> Edit(int id)
        {
            var service = await _context.Services.FindAsync(id);

            if (service == null)
            {
                return NotFound();
            }

            ViewBag.Groups = _context.ServiceGroups
                .OrderBy(g => g.DisplayOrder)
                .ToList();

            return View(service);
        }

        // POST: /Services/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Owner)]
        public async Task<IActionResult> Edit(int id, [Bind("ServiceId,ServiceName,ServiceGroupId,DurationMinutes,Price,Description,IsActive")] Service service)
        {
            if (id != service.ServiceId)
            {
                return NotFound();
            }

            ValidateService(service);

            if (!ModelState.IsValid)
            {
                ViewBag.Groups = _context.ServiceGroups
                    .OrderBy(g => g.DisplayOrder)
                    .ToList();

                return View(service);
            }

            var existing = await _context.Services.FindAsync(id);
            if (existing == null) return NotFound();
            existing.ServiceName = service.ServiceName.Trim();
            existing.ServiceGroupId = service.ServiceGroupId;
            existing.Description = service.Description;
            existing.Price = service.Price;
            existing.DurationMinutes = service.DurationMinutes;
            existing.IsActive = service.IsActive;
            existing.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật dịch vụ";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Services/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Owner)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var service = await _context.Services.FindAsync(id);

            if (service == null)
            {
                return NotFound();
            }

            service.IsActive = !service.IsActive;
            service.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = service.IsActive
                ? "Đã mở bán"
                : "Đã ngừng bán";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Services/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Owner)]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _context.Services
                .Include(s => s.AppointmentServices)
                .FirstOrDefaultAsync(s => s.ServiceId == id);

            if (service == null)
            {
                return NotFound();
            }

            // Chặn xóa nếu đã có lịch hẹn tham chiếu
            if (service.AppointmentServices.Any())
            {
                TempData["Error"] =
                    "Dịch vụ đã có lịch hẹn, chỉ có thể ngừng bán";

                return RedirectToAction(nameof(Index));
            }

            _context.Services.Remove(service);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xoá dịch vụ";

            return RedirectToAction(nameof(Index));
        }

        // Validation dùng chung
        private void ValidateService(Service service)
        {
            if (service.ServiceGroupId.HasValue && !_context.ServiceGroups.Any(g => g.ServiceGroupId == service.ServiceGroupId))
                ModelState.AddModelError(nameof(service.ServiceGroupId), "Nhóm dịch vụ không tồn tại.");
            if (string.IsNullOrWhiteSpace(service.ServiceName))
            {
                ModelState.AddModelError(
                    "ServiceName",
                    "Tên dịch vụ không được để trống");
            }

            if (service.DurationMinutes < 15 ||
                service.DurationMinutes > 240)
            {
                ModelState.AddModelError(
                    "DurationMinutes",
                    "Thời lượng phải từ 15 đến 240 phút");
            }

            if (service.DurationMinutes % 15 != 0)
            {
                ModelState.AddModelError(
                    "DurationMinutes",
                    "Thời lượng phải là bội số của 15 phút");
            }

            if (service.Price <= 0 || decimal.Truncate(service.Price) != service.Price)
            {
                ModelState.AddModelError(
                    "Price",
                    "Giá phải là số nguyên dương");
            }

            if (service.Price > 20000000)
            {
                ModelState.AddModelError(
                    "Price",
                    "Giá tối đa 20.000.000 đ");
            }
        }
    }
}


