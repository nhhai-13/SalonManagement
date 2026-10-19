using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Owner;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class DiscountCodeSliceOneTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    private static DiscountCouponsController CreateController(ApplicationDbContext db)
    {
        var http = new DefaultHttpContext();
        return new DiscountCouponsController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new TestTempDataProvider())
        };
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    [Fact]
    public async Task CreateDiscountCode_ValidPercentageAndFixedAmount_Succeeds()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);

        // 1. Valid 10% discount code
        var percentModel = new DiscountCouponViewModel
        {
            Code = "SALE10",
            DiscountType = "Percent",
            Value = 10,
            StartsOn = DateTime.Today,
            EndsOn = DateTime.Today.AddDays(7)
        };

        var percentResult = await controller.Create(percentModel);
        var percentRedirect = Assert.IsType<RedirectToActionResult>(percentResult);
        Assert.Equal(nameof(DiscountCouponsController.Index), percentRedirect.ActionName);

        var savedPercent = await db.DiscountCoupons.FirstOrDefaultAsync(c => c.Code == "SALE10");
        Assert.NotNull(savedPercent);
        Assert.Equal(10, savedPercent.Value);
        Assert.Equal("Percent", savedPercent.DiscountType);
        Assert.True(savedPercent.IsActive);

        // 2. Valid 50,000 VND fixed amount code
        var fixedModel = new DiscountCouponViewModel
        {
            Code = "GIAM50K",
            DiscountType = "Fixed",
            Value = 50000,
            StartsOn = DateTime.Today,
            EndsOn = DateTime.Today.AddDays(14)
        };

        var fixedResult = await controller.Create(fixedModel);
        var fixedRedirect = Assert.IsType<RedirectToActionResult>(fixedResult);
        Assert.Equal(nameof(DiscountCouponsController.Index), fixedRedirect.ActionName);

        var savedFixed = await db.DiscountCoupons.FirstOrDefaultAsync(c => c.Code == "GIAM50K");
        Assert.NotNull(savedFixed);
        Assert.Equal(50000, savedFixed.Value);
        Assert.Equal("Fixed", savedFixed.DiscountType);
    }

    [Theory]
    [InlineData("", "Percent", 10, 0, 7)] // Missing code
    [InlineData("   ", "Percent", 10, 0, 7)] // Whitespace code
    [InlineData("TEST", "Percent", 0, 0, 7)] // Percentage <= 0
    [InlineData("TEST", "Percent", -5, 0, 7)] // Negative percentage
    [InlineData("TEST", "Percent", 101, 0, 7)] // Percentage > 100
    [InlineData("TEST", "Fixed", 0, 0, 7)] // Fixed amount <= 0
    [InlineData("TEST", "Fixed", -10000, 0, 7)] // Negative fixed amount
    [InlineData("TEST", "InvalidType", 10, 0, 7)] // Invalid discount type
    [InlineData("TEST", "Percent", 10, 5, 2)] // EndDate < StartDate
    public async Task ValidateDiscountCodeCreation_InvalidScenarios_Fails(
        string code, string discountType, decimal value, int startOffsetDays, int endOffsetDays)
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);

        var model = new DiscountCouponViewModel
        {
            Code = code,
            DiscountType = discountType,
            Value = value,
            StartsOn = DateTime.Today.AddDays(startOffsetDays),
            EndsOn = DateTime.Today.AddDays(endOffsetDays)
        };

        var result = await controller.Create(model);
        Assert.False(controller.ModelState.IsValid);
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task CreateDiscountCode_DuplicateCodeCaseInsensitive_Fails()
    {
        using var db = CreateDbContext();
        db.DiscountCoupons.Add(new DiscountCoupon
        {
            Code = "SUMMER2026",
            DiscountType = "Percent",
            Value = 15,
            StartsOn = DateTime.Today,
            EndsOn = DateTime.Today.AddDays(10),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var duplicateModel = new DiscountCouponViewModel
        {
            Code = "summer2026", // Lowercase duplicate
            DiscountType = "Fixed",
            Value = 30000,
            StartsOn = DateTime.Today,
            EndsOn = DateTime.Today.AddDays(5)
        };

        var result = await controller.Create(duplicateModel);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(DiscountCouponViewModel.Code)));
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public void CalculateDiscount_TenPercentOn450k_Reduces45kFinal405k()
    {
        // AC1 & AC3: 10% on 450,000 đ -> discount = 45,000 đ, final = 405,000 đ
        const decimal subtotal = 450000m;
        var discount = DiscountCalculator.Calculate(subtotal, "Percent", 10m);
        var final = subtotal - discount;

        Assert.Equal(45000m, discount);
        Assert.Equal(405000m, final);

        // Also test with "Percentage" alias and helper method
        var (calcDiscount, calcFinal) = DiscountCalculator.CalculateDiscount(subtotal, "Percentage", 10m);
        Assert.Equal(45000m, calcDiscount);
        Assert.Equal(405000m, calcFinal);
    }

    [Fact]
    public void CalculateDiscount_FiftyThousandFixedOn450k_Reduces50kFinal400k()
    {
        // AC1 & AC3: 50,000 đ fixed on 450,000 đ -> discount = 50,000 đ, final = 400,000 đ
        const decimal subtotal = 450000m;
        var discount = DiscountCalculator.Calculate(subtotal, "Fixed", 50000m);
        var final = subtotal - discount;

        Assert.Equal(50000m, discount);
        Assert.Equal(400000m, final);

        // Also test with "FixedAmount" alias and helper method
        var (calcDiscount, calcFinal) = DiscountCalculator.CalculateDiscount(subtotal, "FixedAmount", 50000m);
        Assert.Equal(50000m, calcDiscount);
        Assert.Equal(400000m, calcFinal);
    }

    [Fact]
    public void CalculateDiscount_FixedAmountGreaterThanSubtotal_CapsAtSubtotalAndFinalZeroNotNegative()
    {
        // Constraint: If fixed discount > original amount, DiscountAmount = OriginalAmount, FinalAmount = 0 (never negative)
        const decimal subtotal = 50000m;
        const decimal fixedDiscountValue = 100000m;

        var discount = DiscountCalculator.Calculate(subtotal, "Fixed", fixedDiscountValue);
        var final = subtotal - discount;

        Assert.Equal(50000m, discount); // Capped at subtotal
        Assert.Equal(0m, final);

        var (calcDiscount, calcFinal) = DiscountCalculator.CalculateDiscount(subtotal, "FixedAmount", fixedDiscountValue);
        Assert.Equal(50000m, calcDiscount);
        Assert.Equal(0m, calcFinal);
    }

    [Fact]
    public void CalculateDiscount_NoDiscountCode_ZeroDiscountAndFinalEqualsOriginal()
    {
        const decimal subtotal = 450000m;
        var (calcDiscount, calcFinal) = DiscountCalculator.CalculateDiscount(subtotal, null, null);

        Assert.Equal(0m, calcDiscount);
        Assert.Equal(subtotal, calcFinal);

        var invoice = new Invoice
        {
            Subtotal = subtotal,
            TotalAmount = subtotal,
            DiscountAmount = 0
        };

        Assert.Equal(subtotal, invoice.OriginalAmount);
        Assert.Equal(0m, invoice.DiscountAmount);
        Assert.Equal(subtotal, invoice.FinalAmount);
    }
}
