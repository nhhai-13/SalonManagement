using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Booking;

namespace SalonManagement.Services;

public interface IBookingService
{
    /// <summary>
    /// Giới hạn số lượng dịch vụ tối đa cho một lượt đặt lịch (AC1).
    /// </summary>
    public const int MaxServicesLimit = 5;

    /// <summary>
    /// Tính toán tổng thời lượng và tổng tiền tạm tính từ danh sách thực thể Service (Pure Logic).
    /// </summary>
    BookingTotalsDto CalculateTotals(IEnumerable<Service> selectedServices);

    /// <summary>
    /// Tra cứu database và tính toán lại tổng thời lượng, tổng tiền từ danh sách ServiceId được chọn.
    /// Đảm bảo chỉ tính các dịch vụ đang kinh doanh (IsActive == true).
    /// </summary>
    Task<BookingTotalsDto> CalculateTotalsAsync(IEnumerable<int> selectedServiceIds);

    /// <summary>
    /// Lấy dữ liệu ViewModel cho màn hình chọn dịch vụ của khách hàng.
    /// </summary>
    Task<BookingSelectServicesViewModel> GetSelectServicesViewModelAsync(IEnumerable<int>? preselectedServiceIds = null);
}
