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

        public DateTimeOffset? CheckedInAt { get; set; }
        public int LateMinutes { get; set; }
        public DateTimeOffset? NoShowAt { get; set; }
        public string? StatusBeforeNoShow { get; set; }
        public Guid Version { get; set; } = Guid.NewGuid();

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public Customer Customer { get; set; } = null!;

        public Stylist Stylist { get; set; } = null!;

        public ICollection<AppointmentService> AppointmentServices { get; set; }
            = new List<AppointmentService>();

        public Invoice? Invoice { get; set; }
    }
}
