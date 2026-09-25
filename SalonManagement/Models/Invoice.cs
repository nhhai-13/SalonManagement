namespace SalonManagement.Models
{
    public class Invoice
    {
        public int InvoiceId { get; set; }

        public int AppointmentId { get; set; }

        public decimal Subtotal { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Unpaid";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? CreatedByUserId { get; set; }

        public Appointment Appointment { get; set; } = null!;

        public ICollection<Payment> Payments { get; set; }
            = new List<Payment>();
    }
}