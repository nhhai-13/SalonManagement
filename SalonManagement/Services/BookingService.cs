using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Booking;

namespace SalonManagement.Services;

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _context;
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public BookingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public BookingTotalsDto CalculateTotals(IEnumerable<Service> selectedServices, int? maxShiftDurationMinutes = null)
    {
        var servicesList = selectedServices?.ToList() ?? new List<Service>();

        if (servicesList.Count > IBookingService.MaxServicesLimit)
        {
            throw new ArgumentException($"Số lượng dịch vụ trong một lượt đặt không được vượt quá {IBookingService.MaxServicesLimit}", nameof(selectedServices));
        }

        var maxShift = maxShiftDurationMinutes ?? IBookingService.DefaultMaxShiftDurationMinutes;

        if (servicesList.Count == 0)
        {
            return new BookingTotalsDto
            {
                TotalDurationMinutes = 0,
                FormattedTotalDuration = "0 phút",
                TotalPrice = 0m,
                FormattedTotalPrice = "0 đ",
                SelectedServices = new List<SelectedServiceSummaryItem>(),
                MaxShiftDurationMinutes = maxShift,
                HasExceededShiftWarning = false,
                ShiftWarningMessage = null
            };
        }

        var totalMinutes = servicesList.Sum(s => s.DurationMinutes);
        var totalPrice = servicesList.Sum(s => s.Price);

        var summaryItems = servicesList.Select(s => new SelectedServiceSummaryItem
        {
            ServiceId = s.ServiceId,
            ServiceName = s.ServiceName,
            Price = s.Price,
            FormattedPrice = FormatCurrency(s.Price),
            DurationMinutes = s.DurationMinutes,
            FormattedDuration = FormatDuration(s.DurationMinutes)
        }).ToList();

        var hasExceededShift = totalMinutes > maxShift;
        string? shiftWarningMessage = hasExceededShift
            ? FormatShiftWarningMessage(totalMinutes, maxShift)
            : null;

        return new BookingTotalsDto
        {
            TotalDurationMinutes = totalMinutes,
            FormattedTotalDuration = FormatDuration(totalMinutes),
            TotalPrice = totalPrice,
            FormattedTotalPrice = FormatCurrency(totalPrice),
            SelectedServices = summaryItems,
            MaxShiftDurationMinutes = maxShift,
            HasExceededShiftWarning = hasExceededShift,
            ShiftWarningMessage = shiftWarningMessage
        };
    }

    public async Task<BookingTotalsDto> CalculateTotalsAsync(IEnumerable<int> selectedServiceIds)
    {
        var idList = selectedServiceIds?.Distinct().ToList() ?? new List<int>();

        if (idList.Count > IBookingService.MaxServicesLimit)
        {
            throw new ArgumentException($"Số lượng dịch vụ trong một lượt đặt không được vượt quá {IBookingService.MaxServicesLimit}", nameof(selectedServiceIds));
        }

        var maxShiftMinutes = await GetMaxShiftDurationMinutesAsync();

        if (idList.Count == 0)
        {
            return CalculateTotals(Enumerable.Empty<Service>(), maxShiftMinutes);
        }

        // Chỉ truy vấn các dịch vụ đang kinh doanh (IsActive == true)
        var activeServices = await _context.Services.AsNoTracking()
            .Where(s => s.IsActive && idList.Contains(s.ServiceId))
            .ToListAsync();

        // Giữ thứ tự chọn ban đầu của khách hàng
        var orderedServices = idList
            .Select(id => activeServices.FirstOrDefault(s => s.ServiceId == id))
            .Where(s => s != null)
            .Select(s => s!)
            .ToList();

        return CalculateTotals(orderedServices, maxShiftMinutes);
    }

    public async Task<int> GetMaxShiftDurationMinutesAsync()
    {
        // 1. Kiểm tra WorkSchedules của Stylist
        var scheduleTimes = await _context.WorkSchedules.AsNoTracking()
            .Where(ws => ws.EndTime > ws.StartTime && (ws.Status == "Working" || string.IsNullOrEmpty(ws.Status)))
            .Select(ws => new { ws.StartTime, ws.EndTime })
            .ToListAsync();

        if (scheduleTimes.Count > 0)
        {
            var maxMinutes = scheduleTimes
                .Select(ws => (int)(ws.EndTime - ws.StartTime).TotalMinutes)
                .DefaultIfEmpty(0)
                .Max();

            if (maxMinutes > 0)
            {
                return maxMinutes;
            }
        }

        // 2. Nếu WorkSchedules chưa có, kiểm tra BusinessHours của tiệm
        var openBusinessHours = await _context.BusinessHours.AsNoTracking()
            .Where(bh => !bh.IsClosed && bh.OpensAt != null && bh.ClosesAt != null)
            .Select(bh => new { bh.OpensAt, bh.ClosesAt })
            .ToListAsync();

        if (openBusinessHours.Count > 0)
        {
            var maxBhMinutes = openBusinessHours
                .Where(bh => bh.ClosesAt > bh.OpensAt)
                .Select(bh => (int)(bh.ClosesAt!.Value.ToTimeSpan() - bh.OpensAt!.Value.ToTimeSpan()).TotalMinutes)
                .DefaultIfEmpty(0)
                .Max();

            if (maxBhMinutes > 0)
            {
                return maxBhMinutes;
            }
        }

        // 3. Fallback giá trị mặc định 240 phút (4 giờ)
        return IBookingService.DefaultMaxShiftDurationMinutes;
    }

    public async Task<BookingSelectServicesViewModel> GetSelectServicesViewModelAsync(IEnumerable<int>? preselectedServiceIds = null)
    {
        var preselectedSet = preselectedServiceIds?.ToHashSet() ?? new HashSet<int>();

        var groups = await _context.ServiceGroups.AsNoTracking()
            .OrderBy(g => g.DisplayOrder)
            .ThenBy(g => g.ServiceGroupId)
            .Select(g => new BookingServiceGroupViewModel
            {
                Id = g.ServiceGroupId,
                Name = g.GroupName,
                DisplayOrder = g.DisplayOrder
            })
            .ToListAsync();

        // Chỉ lấy dịch vụ đang hoạt động
        var services = await _context.Services.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.ServiceName)
            .ThenBy(s => s.ServiceId)
            .ToListAsync();

        var byGroup = services.ToLookup(s => s.ServiceGroupId);

        string? errorMessage = null;
        var validPreselected = preselectedSet;
        if (preselectedSet.Count > IBookingService.MaxServicesLimit)
        {
            errorMessage = $"Số lượng dịch vụ trong một lượt đặt không được vượt quá {IBookingService.MaxServicesLimit}";
            validPreselected = preselectedSet.Take(IBookingService.MaxServicesLimit).ToHashSet();
        }

        foreach (var group in groups)
        {
            group.Services = byGroup[group.Id].Select(s => MapToItemViewModel(s, validPreselected)).ToList();
        }

        if (byGroup[null].Any())
        {
            groups.Add(new BookingServiceGroupViewModel
            {
                Id = null,
                Name = "Chưa phân nhóm",
                DisplayOrder = int.MaxValue,
                Services = byGroup[null].Select(s => MapToItemViewModel(s, validPreselected)).ToList()
            });
        }

        var maxShiftMinutes = await GetMaxShiftDurationMinutesAsync();

        // Tính totals ban đầu cho các preselected services
        var totals = await CalculateTotalsAsync(validPreselected);

        return new BookingSelectServicesViewModel
        {
            Groups = groups,
            SelectedServiceIds = validPreselected.ToList(),
            Totals = totals,
            ErrorMessage = errorMessage,
            MaxServicesLimit = IBookingService.MaxServicesLimit,
            MaxShiftDurationMinutes = maxShiftMinutes
        };
    }

    public static string FormatShiftWarningMessage(int totalMinutes, int maxShiftMinutes)
    {
        return $"Tổng thời gian các dịch vụ đã chọn ({totalMinutes} phút) vượt quá độ dài ca làm việc dài nhất của tiệm ({maxShiftMinutes} phút). Quý khách nên cân nhắc tách thành 2 lần hẹn để có trải nghiệm phục vụ tốt nhất.";
    }

    public static string FormatCurrency(decimal price)
    {
        return price.ToString("N0", VietnameseCulture) + " đ";
    }

    public static string FormatDuration(int minutes)
    {
        if (minutes <= 0)
        {
            return "0 phút";
        }

        if (minutes < 60)
        {
            return $"{minutes} phút";
        }

        var hours = minutes / 60;
        var remainingMinutes = minutes % 60;

        if (remainingMinutes == 0)
        {
            return $"{hours} giờ ({minutes} phút)";
        }

        return $"{hours} giờ {remainingMinutes} phút ({minutes} phút)";
    }

    private static BookingServiceItemViewModel MapToItemViewModel(Service s, HashSet<int> preselectedSet)
    {
        return new BookingServiceItemViewModel
        {
            ServiceId = s.ServiceId,
            ServiceName = s.ServiceName,
            Description = s.Description,
            Price = s.Price,
            FormattedPrice = FormatCurrency(s.Price),
            DurationMinutes = s.DurationMinutes,
            FormattedDuration = FormatDuration(s.DurationMinutes),
            ServiceGroupId = s.ServiceGroupId,
            GroupName = s.ServiceGroup?.GroupName ?? "Chưa phân nhóm",
            IsSelected = preselectedSet.Contains(s.ServiceId)
        };
    }

    public static string? NormalizePhone(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84") && digits.Length == 11) digits = "0" + digits[2..];
        return digits.Length == 10 ? digits : null;
    }
}
