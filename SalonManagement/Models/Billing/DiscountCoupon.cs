using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public class DiscountCoupon
{
    public int DiscountCouponId { get; set; }

    [Required, StringLength(32)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(16)]
    public string DiscountType { get; set; } = "Percent";

    public decimal Value { get; set; }

    public DateTime StartsOn { get; set; }

    public DateTime EndsOn { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
