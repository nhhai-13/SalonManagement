using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Owner;

[Authorize(Roles = UserRoles.Owner)]
public sealed class PromotionCodesPageController : Controller
{
    [HttpGet("owner/promotions")]
    public IActionResult Index() => View("~/Views/PromotionCodes/Index.cshtml");
}

[Authorize(Roles = UserRoles.Owner)]
[ApiController]
[Route("api/owner/promotions")]
public sealed class PromotionCodesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok((await db.PromotionCodes.AsNoTracking()
        .OrderByDescending(item => item.IsActive)
        .ThenByDescending(item => item.EndsAt)
        .ToListAsync())
        .Select(item => new PromotionCodeResponse(item.PromotionCodeId, item.Code, item.Name, item.DiscountType, item.DiscountValue, item.StartsAt, item.EndsAt, item.IsActive)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PromotionCodeRequest request)
    {
        var validation = Validate(request);
        if (validation is not null) return BadRequest(new { error = validation });
        var code = request.Code!.Trim().ToUpperInvariant();
        if (await db.PromotionCodes.AnyAsync(item => item.Code == code)) return Conflict(new { error = "Mã giảm giá này đã tồn tại." });

        var promotion = new PromotionCode
        {
            Code = code,
            Name = request.Name!.Trim(),
            DiscountType = request.DiscountType!,
            DiscountValue = request.DiscountValue,
            StartsAt = request.StartsOn!.Value.ToDateTime(TimeOnly.MinValue),
            EndsAt = request.EndsOn!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddTicks(-1),
            IsActive = request.IsActive
        };
        db.PromotionCodes.Add(promotion);
        await db.SaveChangesAsync();
        return Created($"/api/owner/promotions/{promotion.PromotionCodeId}", new PromotionCodeResponse(promotion.PromotionCodeId, promotion.Code, promotion.Name, promotion.DiscountType, promotion.DiscountValue, promotion.StartsAt, promotion.EndsAt, promotion.IsActive));
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] PromotionStatusRequest request)
    {
        var promotion = await db.PromotionCodes.SingleOrDefaultAsync(item => item.PromotionCodeId == id);
        if (promotion is null) return NotFound(new { error = "Không tìm thấy mã giảm giá." });
        promotion.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return Ok(new { message = request.IsActive ? "Đã bật mã giảm giá." : "Đã tạm ngưng mã giảm giá." });
    }

    private static string? Validate(PromotionCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return "Vui lòng nhập mã giảm giá.";
        if (request.Code.Trim().Length > 64) return "Mã giảm giá tối đa 64 ký tự.";
        if (string.IsNullOrWhiteSpace(request.Name)) return "Vui lòng nhập tên chương trình.";
        if (request.DiscountType is not ("Percentage" or "FixedAmount")) return "Kiểu giảm giá không hợp lệ.";
        if (request.DiscountValue <= 0) return "Giá trị giảm phải lớn hơn 0.";
        if (request.DiscountType == "Percentage" && request.DiscountValue > 100) return "Phần trăm giảm giá không được vượt quá 100%.";
        if (request.StartsOn is null || request.EndsOn is null) return "Vui lòng chọn ngày bắt đầu và kết thúc.";
        if (request.EndsOn < request.StartsOn) return "Ngày kết thúc phải từ ngày bắt đầu trở đi.";
        return null;
    }
}

public sealed record PromotionCodeRequest(string? Code, string? Name, string? DiscountType, decimal DiscountValue, DateOnly? StartsOn, DateOnly? EndsOn, bool IsActive = true);
public sealed record PromotionStatusRequest(bool IsActive);
public sealed record PromotionCodeResponse(int Id, string Code, string Name, string DiscountType, decimal DiscountValue, DateTime StartsAt, DateTime EndsAt, bool IsActive);
