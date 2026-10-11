namespace SalonManagement.Models
{
    public class AppointmentService
    {
        public int AppointmentServiceId { get; set; }

        public int AppointmentId { get; set; }

        public int ServiceId { get; set; }

        public decimal Price { get; set; }

        public int DurationMinutes { get; set; }

        public Appointment Appointment { get; set; } = null!;

        public Service Service { get; set; } = null!;
    }
}
