using System;

namespace SalonManagement.Models
{
    public class AppointmentChangeLog
    {
        public int Id { get; set; }

        public int AppointmentId { get; set; }

        public string ModifiedByUserId { get; set; } = string.Empty;

        public string? ModifiedByUserName { get; set; }

        public int OldStylistId { get; set; }

        public int NewStylistId { get; set; }

        public string? OldStylistName { get; set; }

        public string? NewStylistName { get; set; }

        public DateTime OldDate { get; set; }

        public DateTime NewDate { get; set; }

        public TimeSpan OldStartTime { get; set; }

        public TimeSpan NewStartTime { get; set; }

        public TimeSpan OldEndTime { get; set; }

        public TimeSpan NewEndTime { get; set; }

        public string? Reason { get; set; }

        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Appointment? Appointment { get; set; }
    }
}
