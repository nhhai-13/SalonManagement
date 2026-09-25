namespace SalonManagement.Models
{
    public class Service
    {
        public int ServiceId { get; set; }

        public string ServiceName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public int DurationMinutes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<AppointmentService> AppointmentServices { get; set; }
            = new List<AppointmentService>();
    }
}