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

    public BookingTotalsDto CalculateTotals(IEnumerable<Service> selectedServices)
    {
        var servicesList = selectedServices?.ToList() ?? new List<Service>();

        if (servicesList.Count > IBookingService.MaxServicesLimit)
        {
            throw new ArgumentException($"Số lượng dịch vụ trong một lượt đặt không được vượt quá {IBookingService.MaxServicesLimit}", nameof(selectedServices));
        }

        if (servicesList.Count == 0)
        {
            return new BookingTotalsDto
            {
                TotalDurationMinutes = 0,
                FormattedTotalDuration = "0 phút",
                TotalPrice = 0m,
                FormattedTotalPrice = "0 đ",
                SelectedServices = new List<SelectedServiceSummaryItem>()
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

        return new BookingTotalsDto
        {
            TotalDurationMinutes = totalMinutes,
            FormattedTotalDuration = FormatDuration(totalMinutes),
            TotalPrice = totalPrice,
            FormattedTotalPrice = FormatCurrency(totalPrice),
            SelectedServices = summaryItems
        };
    }

    public async Task<BookingTotalsDto> CalculateTotalsAsync(IEnumerable<int> selectedServiceIds)
    {
        var idList = selectedServiceIds?.Distinct().ToList() ?? new List<int>();

        if (idList.Count > IBookingService.MaxServicesLimit)
        {
            throw new ArgumentException($"Số lượng dịch vụ trong một lượt đặt không được vượt quá {IBookingService.MaxServicesLimit}", nameof(selectedServiceIds));
        }

        if (idList.Count == 0)
        {
            return CalculateTotals(Enumerable.Empty<Service>());
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

        return CalculateTotals(orderedServices);
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

        // Tính totals ban đầu cho các preselected services
        var totals = await CalculateTotalsAsync(validPreselected);

        return new BookingSelectServicesViewModel
        {
            Groups = groups,
            SelectedServiceIds = validPreselected.ToList(),
            Totals = totals,
            ErrorMessage = errorMessage,
            MaxServicesLimit = IBookingService.MaxServicesLimit
        };
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
}
