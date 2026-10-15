namespace SalonManagement.Models
{
    /// <summary>
    /// Một khung giờ khả dụng để khách chọn đặt lịch.
    /// </summary>
    public class AvailabilitySlot
    {
        /// <summary>Giờ bắt đầu (VD: 09:00)</summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>Giờ kết thúc dự kiến = StartTime + totalDuration</summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>Danh sách thợ có thể nhận slot này (nếu khách chọn "thợ bất kỳ")</summary>
        public List<int> AvailableStylistIds { get; set; } = new();

        /// <summary>Hiển thị cho FE: "09:00"</summary>
        public string DisplayTime => $"{StartTime.Hours:D2}:{StartTime.Minutes:D2}";
    }
}