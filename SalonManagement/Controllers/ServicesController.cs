using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

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
        public async Task<IActionResult> Index()
        {
            var services = await _context.Services
                .Include(s => s.ServiceGroup)
                .OrderBy(s => s.ServiceGroup!.DisplayOrder)
                .ThenBy(s => s.ServiceName)
                .ToListAsync();

            return View(services);
        }

        // GET: /Services/Create
        [Authorize(Roles = RoleGroups.Management)]
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
        [Authorize(Roles = RoleGroups.Management)]
        public async Task<IActionResult> Create(Service service)
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
        [Authorize(Roles = RoleGroups.Management)]
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
        [Authorize(Roles = RoleGroups.Management)]
        public async Task<IActionResult> Edit(int id, Service service)
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

            service.UpdatedAt = DateTime.Now;

            _context.Update(service);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã cập nhật dịch vụ";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Services/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleGroups.Management)]
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
        [Authorize(Roles = RoleGroups.Management)]
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

            if (service.Price <= 0)
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