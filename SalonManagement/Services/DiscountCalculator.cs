using SalonManagement.Models;

namespace SalonManagement.Services;

public static class DiscountCalculator
{
    public static decimal Calculate(decimal subtotal, string type, decimal value)
    {
        if (subtotal <= 0 || value <= 0) return 0;
        var discount = type switch
        {
            "Percent" or "Percentage" when value <= 100 => Math.Round(subtotal * value / 100m, 2, MidpointRounding.AwayFromZero),
            "Fixed" or "FixedAmount" => value,
            _ => throw new ArgumentOutOfRangeException(nameof(value), "Coupon value or type is invalid.")
        };
        return Math.Min(subtotal, discount);
    }

    public static (decimal DiscountAmount, decimal FinalAmount) CalculateDiscount(decimal originalAmount, string? type, decimal? value)
    {
        if (originalAmount <= 0 || string.IsNullOrWhiteSpace(type) || !value.HasValue || value.Value <= 0)
        {
            return (0, Math.Max(0, originalAmount));
        }

        var discount = Calculate(originalAmount, type, value.Value);
        var final = Math.Max(0, originalAmount - discount);
        return (discount, final);
    }

    public static (decimal DiscountAmount, decimal FinalAmount) CalculateDiscount(decimal originalAmount, DiscountCoupon? coupon)
    {
        if (coupon is null) return (0, Math.Max(0, originalAmount));
        return CalculateDiscount(originalAmount, coupon.DiscountType, coupon.Value);
    }

    public static (decimal DiscountAmount, decimal FinalAmount) CalculateDiscount(decimal originalAmount, DiscountCode? discountCode)
    {
        if (discountCode is null) return (0, Math.Max(0, originalAmount));
        return CalculateDiscount(originalAmount, discountCode.DiscountType, discountCode.DiscountValue);
    }
}
