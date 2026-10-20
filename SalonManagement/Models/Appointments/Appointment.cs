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
        public DateTimeOffset? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? CancelReason { get; set; }
        public string? ConfirmedByUserId { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string? RejectedByUserId { get; set; }
        public DateTime? RejectedAt { get; set; }
        public DateTimeOffset? ServiceStartedAt { get; set; }
        public DateTimeOffset? ServiceCompletedAt { get; set; }
        public string Source { get; set; } = "Online";
        public DateTimeOffset? CheckedInAt { get; set; }
        public int LateMinutes { get; set; }
        public DateTimeOffset? NoShowAt { get; set; }
        public string? StatusBeforeNoShow { get; set; }
        public Guid Version { get; set; } = Guid.NewGuid();
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ActualStartedAt { get; set; }
        public DateTime? ActualCompletedAt { get; set; }
        // Internal stylist note: never returned by public booking or lookup flows.
        public string? TechnicalNotes { get; set; }

        public DateTime? ArrivedAt { get; set; }

        public bool IsLate { get; set; }

        public string? NoShowByUserId { get; set; }

        public DateTime? ClosedAt { get; set; }

        public string? ClosedByUserId { get; set; }

        public string? ClosedByName { get; set; }
        public Customer Customer { get; set; } = null!;
        public Stylist Stylist { get; set; } = null!;
        public ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
        public Invoice? Invoice { get; set; }
        public ServiceReview? ServiceReview { get; set; }
        public ICollection<AppointmentEmailNotification> EmailNotifications { get; set; } = new List<AppointmentEmailNotification>();
        public ScheduleContactFollowUp? ScheduleContactFollowUp { get; set; }
        public ICollection<ScheduleChangeNotification> ScheduleChangeNotifications { get; set; } = new List<ScheduleChangeNotification>();
        public ICollection<AppointmentEmailDelivery> EmailDeliveries { get; set; } = new List<AppointmentEmailDelivery>();
        // Thêm 3 trường này để lưu trạng thái gửi email xác nhận (S4-05)
        public string? ConfirmationEmailStatus { get; set; } // "NotSent", "Sent", "Failed", "NoEmail"
        public DateTime? ConfirmationEmailSentAt { get; set; }
        public string? ConfirmationEmailError { get; set; }
    }
}
