namespace SalonManagement.Models.ViewModels.Booking;

public class SelectedServiceSummaryItem
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string FormattedDuration { get; set; } = string.Empty;
}

public class BookingTotalsDto
{
    public int TotalDurationMinutes { get; set; }
    public string FormattedTotalDuration { get; set; } = "0 phút";
    public decimal TotalPrice { get; set; }
    public string FormattedTotalPrice { get; set; } = "0 đ";
    public string PriceNote { get; set; } = "Giá trên là giá tạm tính, giá cuối do tiệm chốt khi thanh toán";
    public List<SelectedServiceSummaryItem> SelectedServices { get; set; } = new();
}

public class CalculateBookingTotalsRequest
{
    public List<int> ServiceIds { get; set; } = new();
}

public class BookingServiceItemViewModel
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string FormattedPrice { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string FormattedDuration { get; set; } = string.Empty;
    public int? ServiceGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class BookingServiceGroupViewModel
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<BookingServiceItemViewModel> Services { get; set; } = new();
}

public class BookingSelectServicesViewModel
{
    public int MaxServicesLimit { get; set; } = 5;
    public string? ErrorMessage { get; set; }
    public List<BookingServiceGroupViewModel> Groups { get; set; } = new();
    public List<int> SelectedServiceIds { get; set; } = new();
    public BookingTotalsDto Totals { get; set; } = new();
    public bool HasServices => Groups.Any(g => g.Services.Any());
}
