using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
public class DiscountCouponsController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var coupons = await db.DiscountCoupons.AsNoTracking()
            .OrderByDescending(item => item.StartsOn).ThenBy(item => item.Code)
            .Select(item => new DiscountCouponListItem(item.DiscountCouponId, item.Code, item.DiscountType, item.Value,
                item.StartsOn, item.EndsOn, item.Invoices.Count, item.Invoices.Sum(invoice => (decimal?)invoice.DiscountAmount) ?? 0))
            .ToListAsync();
        return View(coupons);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DiscountCouponViewModel model)
    {
        await ValidateCoupon(model);
        if (!ModelState.IsValid)
        {
            ViewBag.Error = string.Join(" ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));
            return await Index();
        }
        db.DiscountCoupons.Add(new DiscountCoupon { Code = model.Code, DiscountType = model.DiscountType, Value = model.Value,
            StartsOn = model.StartsOn.Date, EndsOn = model.EndsOn.Date });
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã tạo mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, DiscountCouponViewModel model)
    {
        var coupon = await db.DiscountCoupons.Include(item => item.Invoices)
            .FirstOrDefaultAsync(item => item.DiscountCouponId == id);
        if (coupon is null) return NotFound();
        if (coupon.Invoices.Count != 0)
        {
            TempData["Error"] = "Mã đã được áp vào phiếu; không thể sửa để giữ nguyên lịch sử đã chốt.";
            return RedirectToAction(nameof(Index));
        }
        await ValidateCoupon(model, id);
        if (!ModelState.IsValid)
        {
            ViewBag.Error = string.Join(" ", ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));
            return await Index();
        }
        coupon.Code = model.Code;
        coupon.DiscountType = model.DiscountType;
        coupon.Value = model.Value;
        coupon.StartsOn = model.StartsOn.Date;
        coupon.EndsOn = model.EndsOn.Date;
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var coupon = await db.DiscountCoupons.Include(item => item.Invoices)
            .FirstOrDefaultAsync(item => item.DiscountCouponId == id);
        if (coupon is null) return NotFound();
        if (coupon.Invoices.Count != 0)
        {
            TempData["Error"] = "Không thể xóa mã giảm giá đã được dùng trên phiếu.";
            return RedirectToAction(nameof(Index));
        }
        db.DiscountCoupons.Remove(coupon);
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateCoupon(DiscountCouponViewModel model, int? exceptId = null)
    {
        model.Code = model.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (model.DiscountType is not ("Percent" or "Fixed")) ModelState.AddModelError(nameof(model.DiscountType), "Chọn kiểu giảm hợp lệ.");
        if (model.DiscountType == "Percent" && (model.Value < 1 || model.Value > 100)) ModelState.AddModelError(nameof(model.Value), "Phần trăm giảm phải từ 1 đến 100.");
        if (model.StartsOn == default) ModelState.AddModelError(nameof(model.StartsOn), "Ngày bắt đầu là bắt buộc.");
        if (model.EndsOn == default) ModelState.AddModelError(nameof(model.EndsOn), "Ngày kết thúc là bắt buộc.");
        if (model.EndsOn.Date < model.StartsOn.Date) ModelState.AddModelError(nameof(model.EndsOn), "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.");
        if (await db.DiscountCoupons.AnyAsync(item => item.Code == model.Code && item.DiscountCouponId != exceptId))
            ModelState.AddModelError(nameof(model.Code), "Mã này đã tồn tại.");
    }
}

public sealed record DiscountCouponListItem(int Id, string Code, string DiscountType, decimal Value,
    DateTime StartsOn, DateTime EndsOn, int UseCount, decimal TotalDiscount);
