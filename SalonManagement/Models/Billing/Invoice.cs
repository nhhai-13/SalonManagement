namespace SalonManagement.Models
{
    public class Invoice
    {
        public int InvoiceId { get; set; }

        public int AppointmentId { get; set; }

        public decimal Subtotal { get; set; }

        public decimal DiscountAmount { get; set; }

        public int? DiscountCouponId { get; set; }

        public string? DiscountCodeSnapshot { get; set; }

        public string? DiscountTypeSnapshot { get; set; }

        public decimal? DiscountValueSnapshot { get; set; }

        public DiscountCoupon? DiscountCoupon { get; set; }

        public decimal TotalAmount { get; set; }

        /// <summary>Snapshot of the promotion code used at checkout; null when no discount was used.</summary>
        public string? PromotionCode { get; set; }

        public string Status { get; set; } = "Unpaid";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? CreatedByUserId { get; set; }

        public DateTime? ClosedAt { get; set; }

        public string? ClosedByUserId { get; set; }

        public string? ClosedByName { get; set; }

        public Appointment Appointment { get; set; } = null!;

        public ICollection<Payment> Payments { get; set; }
            = new List<Payment>();

        public ICollection<InvoiceLine> Lines { get; set; }
            = new List<InvoiceLine>();

        public ICollection<InvoiceAdjustment> Adjustments { get; set; }
            = new List<InvoiceAdjustment>();

        public decimal AdjustedTotal => TotalAmount + Adjustments.Sum(item => item.AmountDelta);
    }
}
