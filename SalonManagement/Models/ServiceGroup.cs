namespace SalonManagement.Models
{
    public class ServiceGroup
    {
        public int ServiceGroupId { get; set; }

        public string GroupName { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        // Navigation property
        public ICollection<Service> Services { get; set; } = new List<Service>();
    }
}