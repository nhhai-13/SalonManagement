using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Controllers.Owner;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Tests;

[TestClass]
public sealed class PromotionCodesTests
{
    [TestMethod]
    public async Task GetAll_ReturnsPromotionsOrderedByActiveAndEndsAt()
    {
        await using var db = CreateDb();
        var today = DateTime.Today;
        db.PromotionCodes.AddRange(
            new PromotionCode { Code = "INACTIVE", Name = "Hết hạn", DiscountType = "Percentage", DiscountValue = 10, StartsAt = today.AddDays(-10), EndsAt = today.AddDays(-1), IsActive = false },
            new PromotionCode { Code = "ACTIVE_SOON", Name = "Sắp hết", DiscountType = "Percentage", DiscountValue = 15, StartsAt = today, EndsAt = today.AddDays(2), IsActive = true },
            new PromotionCode { Code = "ACTIVE_LATER", Name = "Dài hạn", DiscountType = "FixedAmount", DiscountValue = 50000, StartsAt = today, EndsAt = today.AddDays(30), IsActive = true });
        await db.SaveChangesAsync();

        var controller = new PromotionCodesController(db);
        var actionResult = await controller.GetAll();

        var ok = actionResult as OkObjectResult;
        Assert.IsNotNull(ok);
        var items = (ok.Value as IEnumerable<PromotionCodeResponse>)?.ToList();
        Assert.IsNotNull(items);
        Assert.AreEqual(3, items.Count);
        Assert.AreEqual("ACTIVE_LATER", items[0].Code);
        Assert.AreEqual("ACTIVE_SOON", items[1].Code);
        Assert.AreEqual("INACTIVE", items[2].Code);
    }

    [TestMethod]
    public async Task Create_ValidPercentagePromotion_SucceedsAndNormalizesCode()
    {
        await using var db = CreateDb();
        var controller = new PromotionCodesController(db);
        var start = DateOnly.FromDateTime(DateTime.Today);
        var end = start.AddDays(14);

        var request = new PromotionCodeRequest("  promo10  ", "Giảm 10% mùa hè", "Percentage", 10m, start, end, true);
        var result = await controller.Create(request);

        var created = result as CreatedResult;
        Assert.IsNotNull(created);
        var body = created.Value as PromotionCodeResponse;
        Assert.IsNotNull(body);
        Assert.AreEqual("PROMO10", body.Code);
        Assert.AreEqual(10m, body.DiscountValue);

        var saved = await db.PromotionCodes.SingleAsync();
        Assert.AreEqual("PROMO10", saved.Code);
        Assert.AreEqual("Giảm 10% mùa hè", saved.Name);
    }

    [TestMethod]
    public async Task Create_ValidFixedAmountPromotion_Succeeds()
    {
        await using var db = CreateDb();
        var controller = new PromotionCodesController(db);
        var start = DateOnly.FromDateTime(DateTime.Today);
        var end = start.AddDays(7);

        var request = new PromotionCodeRequest("VOUCHER50K", "Giảm 50K", "FixedAmount", 50000m, start, end, true);
        var result = await controller.Create(request);

        Assert.IsInstanceOfType<CreatedResult>(result);
        var saved = await db.PromotionCodes.SingleAsync();
        Assert.AreEqual("FixedAmount", saved.DiscountType);
        Assert.AreEqual(50000m, saved.DiscountValue);
    }

    [TestMethod]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        await using var db = CreateDb();
        db.PromotionCodes.Add(new PromotionCode
        {
            Code = "WELCOME",
            Name = "Chào mừng",
            DiscountType = "Percentage",
            DiscountValue = 10,
            StartsAt = DateTime.Today,
            EndsAt = DateTime.Today.AddDays(10),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = new PromotionCodesController(db);
        var start = DateOnly.FromDateTime(DateTime.Today);
        var request = new PromotionCodeRequest("welcome", "Khách mới", "Percentage", 15m, start, start.AddDays(5), true);
        var result = await controller.Create(request);

        Assert.IsInstanceOfType<ConflictObjectResult>(result);
    }

    [TestMethod]
    public async Task Create_InvalidInputs_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var controller = new PromotionCodesController(db);
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Code rỗng
        var r1 = await controller.Create(new PromotionCodeRequest("", "Tên", "Percentage", 10m, today, today.AddDays(1)));
        Assert.IsInstanceOfType<BadRequestObjectResult>(r1);

        // Kiểu giảm giá sai
        var r2 = await controller.Create(new PromotionCodeRequest("CODE1", "Tên", "InvalidType", 10m, today, today.AddDays(1)));
        Assert.IsInstanceOfType<BadRequestObjectResult>(r2);

        // Giá trị giảm <= 0
        var r3 = await controller.Create(new PromotionCodeRequest("CODE2", "Tên", "Percentage", 0m, today, today.AddDays(1)));
        Assert.IsInstanceOfType<BadRequestObjectResult>(r3);

        // Phần trăm > 100%
        var r4 = await controller.Create(new PromotionCodeRequest("CODE3", "Tên", "Percentage", 101m, today, today.AddDays(1)));
        Assert.IsInstanceOfType<BadRequestObjectResult>(r4);

        // Ngày kết thúc trước ngày bắt đầu
        var r5 = await controller.Create(new PromotionCodeRequest("CODE4", "Tên", "Percentage", 10m, today, today.AddDays(-1)));
        Assert.IsInstanceOfType<BadRequestObjectResult>(r5);
    }

    [TestMethod]
    public async Task ChangeStatus_TogglesActiveFlag()
    {
        await using var db = CreateDb();
        var promo = new PromotionCode
        {
            Code = "TESTTOGGLE",
            Name = "Bật tắt",
            DiscountType = "Percentage",
            DiscountValue = 10,
            StartsAt = DateTime.Today,
            EndsAt = DateTime.Today.AddDays(5),
            IsActive = true
        };
        db.PromotionCodes.Add(promo);
        await db.SaveChangesAsync();

        var controller = new PromotionCodesController(db);
        var pause = await controller.ChangeStatus(promo.PromotionCodeId, new PromotionStatusRequest(false));
        Assert.IsInstanceOfType<OkObjectResult>(pause);
        Assert.IsFalse((await db.PromotionCodes.SingleAsync()).IsActive);

        var resume = await controller.ChangeStatus(promo.PromotionCodeId, new PromotionStatusRequest(true));
        Assert.IsInstanceOfType<OkObjectResult>(resume);
        Assert.IsTrue((await db.PromotionCodes.SingleAsync()).IsActive);

        var notFound = await controller.ChangeStatus(9999, new PromotionStatusRequest(true));
        Assert.IsInstanceOfType<NotFoundObjectResult>(notFound);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }
}
