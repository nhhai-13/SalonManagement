using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalonManagement.Models;

public class DiscountCoupon
{
    public int DiscountCouponId { get; set; }

    [NotMapped]
    public int Id => DiscountCouponId;

    [Required, StringLength(32)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(16)]
    public string DiscountType { get; set; } = "Percent";

    public decimal Value { get; set; }

    [NotMapped]
    public decimal DiscountValue
    {
        get => Value;
        set => Value = value;
    }

    public DateTime StartsOn { get; set; }

    [NotMapped]
    public DateTime StartDate
    {
        get => StartsOn;
        set => StartsOn = value;
    }

    public DateTime EndsOn { get; set; }

    [NotMapped]
    public DateTime EndDate
    {
        get => EndsOn;
        set => EndsOn = value;
    }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
