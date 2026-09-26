namespace SalonManagement.Models
{
    public class WorkSchedule
    {
        public int WorkScheduleId { get; set; }

        public int StylistId { get; set; }

        public DateTime WorkDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public string Status { get; set; } = "Working";

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Stylist Stylist { get; set; } = null!;
    }
}