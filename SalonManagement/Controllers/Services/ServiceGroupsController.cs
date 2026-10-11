using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[Authorize(Roles = UserRoles.Owner)]
public class ServiceGroupsController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index() => View(await context.ServiceGroups
        .AsNoTracking().Include(g => g.Services).OrderBy(g => g.DisplayOrder)
        .ThenBy(g => g.ServiceGroupId).ToListAsync());

    public IActionResult Create() => View("Edit", new ServiceGroupInput());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceGroupInput input)
    {
        Validate(input);
        if (!ModelState.IsValid) return View("Edit", input);
        context.ServiceGroups.Add(new ServiceGroup { GroupName = input.GroupName.Trim(), DisplayOrder = input.DisplayOrder });
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã thêm nhóm dịch vụ.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var group = await context.ServiceGroups.FindAsync(id);
        return group == null ? NotFound() : View(new ServiceGroupInput
        { ServiceGroupId = id, GroupName = group.GroupName, DisplayOrder = group.DisplayOrder });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ServiceGroupInput input)
    {
        if (id != input.ServiceGroupId) return NotFound();
        var group = await context.ServiceGroups.FindAsync(id);
        if (group == null) return NotFound();
        Validate(input);
        if (!ModelState.IsValid) return View(input);
        group.GroupName = input.GroupName.Trim();
        group.DisplayOrder = input.DisplayOrder;
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật nhóm dịch vụ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var group = await context.ServiceGroups.Include(g => g.Services)
            .SingleOrDefaultAsync(g => g.ServiceGroupId == id);
        if (group == null) return NotFound();
        var activeCount = group.Services.Count(s => s.IsActive);
        if (activeCount > 0)
        {
            TempData["Error"] = $"Không thể xoá nhóm: có {group.Services.Count} dịch vụ thuộc nhóm, trong đó {activeCount} dịch vụ đang bán.";
            return RedirectToAction(nameof(Index));
        }
        foreach (var service in group.Services.ToList())
        {
            service.ServiceGroupId = null;
            service.ServiceGroup = null;
        }
        context.ServiceGroups.Remove(group);
        await context.SaveChangesAsync();
        TempData["Success"] = "Đã xoá nhóm. Dịch vụ ngừng bán được giữ lại trong mục Chưa phân nhóm.";
        return RedirectToAction(nameof(Index));
    }

    private void Validate(ServiceGroupInput input)
    {
        if (string.IsNullOrWhiteSpace(input.GroupName) || input.GroupName.Trim().Length > 100)
            ModelState.AddModelError(nameof(input.GroupName), "Tên nhóm phải có từ 1 đến 100 ký tự.");
        if (input.DisplayOrder < 0)
            ModelState.AddModelError(nameof(input.DisplayOrder), "Thứ tự hiển thị phải từ 0 trở lên.");
    }
}
