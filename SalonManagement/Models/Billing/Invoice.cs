using System.ComponentModel.DataAnnotations.Schema;

namespace SalonManagement.Models
{
    public class Invoice
    {
        public int InvoiceId { get; set; }

        public int AppointmentId { get; set; }

        public decimal Subtotal { get; set; }

        [NotMapped]
        public decimal OriginalAmount
        {
            get => Subtotal;
            set => Subtotal = value;
        }

        public decimal DiscountAmount { get; set; }

        public int? DiscountCouponId { get; set; }

        [NotMapped]
        public int? DiscountCodeId
        {
            get => DiscountCouponId;
            set => DiscountCouponId = value;
        }

        public string? DiscountCodeSnapshot { get; set; }

        [NotMapped]
        public string? AppliedDiscountCode
        {
            get => DiscountCodeSnapshot;
            set => DiscountCodeSnapshot = value;
        }

        public string? DiscountTypeSnapshot { get; set; }

        [NotMapped]
        public string? AppliedDiscountType
        {
            get => DiscountTypeSnapshot;
            set => DiscountTypeSnapshot = value;
        }

        public decimal? DiscountValueSnapshot { get; set; }

        [NotMapped]
        public decimal? AppliedDiscountValue
        {
            get => DiscountValueSnapshot;
            set => DiscountValueSnapshot = value;
        }

        public DiscountCoupon? DiscountCoupon { get; set; }

        public decimal TotalAmount { get; set; }

        [NotMapped]
        public decimal FinalAmount
        {
            get => TotalAmount;
            set => TotalAmount = value;
        }

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
