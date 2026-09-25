namespace SalonManagement.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int InvoiceId { get; set; }

        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = "Pending";

        public DateTime? PaidAt { get; set; }

        public string? TransactionCode { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Invoice Invoice { get; set; } = null!;
    }
}