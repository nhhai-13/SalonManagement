namespace SalonManagement.Models
{
    public class Appointment
    {
        public int AppointmentId { get; set; }

        public int CustomerId { get; set; }

        public int StylistId { get; set; }

        public DateTime AppointmentDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public string Status { get; set; } = "Pending";

        public string BookingReference { get; set; } = string.Empty;

        public string? BookingSource { get; set; }

        public string? CustomerNameOverride { get; set; }

        public string? NameConflictResolution { get; set; }

        public string? Notes { get; set; }

        // Internal stylist note: never returned by the public booking or lookup flows.
        public string? TechnicalNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
        public DateTime? ActualStartedAt { get; set; }
        public DateTime? ActualCompletedAt { get; set; }

        public DateTime? ArrivedAt { get; set; }

        public bool IsLate { get; set; }

        public int? LateMinutes { get; set; }

        public DateTime? NoShowAt { get; set; }

        public string? NoShowByUserId { get; set; }

        public string? ConfirmedByUserId { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public string? RejectionReason { get; set; }

        public string? RejectedByUserId { get; set; }

        public DateTime? RejectedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public string? CancelledBy { get; set; }

        public Customer Customer { get; set; } = null!;

        public Stylist Stylist { get; set; } = null!;

        public ICollection<AppointmentService> AppointmentServices { get; set; }
            = new List<AppointmentService>();

        public Invoice? Invoice { get; set; }
    }
}
