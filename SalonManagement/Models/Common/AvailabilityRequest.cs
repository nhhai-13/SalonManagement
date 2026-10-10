namespace SalonManagement.Models
{
    /// <summary>
    /// Request từ FE để lấy slot khả dụng.
    /// </summary>
    public class AvailabilityRequest
    {
        public DateTime Date { get; set; }
        public List<int> ServiceIds { get; set; } = new();
        public int? StylistId { get; set; }  // null = thợ bất kỳ
    }
}