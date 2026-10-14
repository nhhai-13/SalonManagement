namespace SalonManagement.Models
{
    public class AppointmentService
    {
        public int AppointmentServiceId { get; set; }

        public int AppointmentId { get; set; }

        public int ServiceId { get; set; }

        public int? StylistId { get; set; }

        public decimal Price { get; set; }

        public int DurationMinutes { get; set; }

        public bool IsCompleted { get; set; }

        public decimal CollectedAmount { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string? TechnicalNotes { get; set; }

        public string? TechnicalNotesUpdatedBy { get; set; }

        public DateTime? TechnicalNotesUpdatedAt { get; set; }

        public Appointment Appointment { get; set; } = null!;

        public Service Service { get; set; } = null!;

        public Stylist? Stylist { get; set; }

        public InvoiceLine? InvoiceLine { get; set; }
    }
}
