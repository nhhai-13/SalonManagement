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
    /// Ngưỡng độ dài ca làm việc tiêu chuẩn mặc định khi chưa có cấu hình (240 phút = 4 giờ) (AC4).
    /// </summary>
    public const int DefaultMaxShiftDurationMinutes = 240;

    /// <summary>
    /// Tính toán tổng thời lượng và tổng tiền tạm tính từ danh sách thực thể Service (Pure Logic).
    /// Hỗ trợ kiểm tra cảnh báo nếu vượt quá ca dài nhất (AC4).
    /// </summary>
    BookingTotalsDto CalculateTotals(IEnumerable<Service> selectedServices, int? maxShiftDurationMinutes = null);

    /// <summary>
    /// Tra cứu database và tính toán lại tổng thời lượng, tổng tiền từ danh sách ServiceId được chọn.
    /// Đảm bảo chỉ tính các dịch vụ đang kinh doanh (IsActive == true).
    /// Tự động lấy độ dài ca dài nhất trong tuần để kiểm tra cảnh báo (AC4).
    /// </summary>
    Task<BookingTotalsDto> CalculateTotalsAsync(IEnumerable<int> selectedServiceIds);

    /// <summary>
    /// Lấy độ dài ca làm việc dài nhất trong tuần của tiệm tính bằng phút (AC4).
    /// </summary>
    Task<int> GetMaxShiftDurationMinutesAsync();

    /// <summary>
    /// Lấy dữ liệu ViewModel cho màn hình chọn dịch vụ của khách hàng.
    /// </summary>
    Task<BookingSelectServicesViewModel> GetSelectServicesViewModelAsync(IEnumerable<int>? preselectedServiceIds = null);
}
