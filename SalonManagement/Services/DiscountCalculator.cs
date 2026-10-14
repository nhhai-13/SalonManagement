namespace SalonManagement.Services;

public static class DiscountCalculator
{
    public static decimal Calculate(decimal subtotal, string type, decimal value)
    {
        if (subtotal <= 0 || value <= 0) return 0;
        var discount = type switch
        {
            "Percent" when value <= 100 => Math.Round(subtotal * value / 100m, 2, MidpointRounding.AwayFromZero),
            "Fixed" => value,
            _ => throw new ArgumentOutOfRangeException(nameof(value), "Coupon value or type is invalid.")
        };
        return Math.Min(subtotal, discount);
    }
}
