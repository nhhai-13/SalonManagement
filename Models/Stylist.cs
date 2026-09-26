namespace SalonManagement.Models
{
    public class Stylist
    {
        public int StylistId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? Specialty { get; set; }

        public int? ExperienceYears { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<WorkSchedule> WorkSchedules { get; set; }
            = new List<WorkSchedule>();

        public ICollection<Appointment> Appointments { get; set; }
            = new List<Appointment>();
    }
}