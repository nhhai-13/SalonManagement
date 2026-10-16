namespace SalonManagement.Models;

public class DiscountCode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static DiscountCode FromCoupon(DiscountCoupon coupon)
    {
        return new DiscountCode
        {
            Id = coupon.DiscountCouponId,
            Code = coupon.Code,
            DiscountType = coupon.DiscountType,
            DiscountValue = coupon.Value,
            StartDate = coupon.StartsOn,
            EndDate = coupon.EndsOn,
            IsActive = coupon.IsActive,
            CreatedAt = coupon.CreatedAt
        };
    }
}
