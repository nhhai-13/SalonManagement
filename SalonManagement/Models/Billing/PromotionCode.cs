namespace SalonManagement.Models;

/// <summary>Owner-configured promotion that can be applied once to a completed POS invoice.</summary>
public sealed class PromotionCode
{
    public int PromotionCodeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Percentage or FixedAmount.</summary>
    public string DiscountType { get; set; } = "Percentage";
    public decimal DiscountValue { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
