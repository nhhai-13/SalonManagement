using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Booking;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Booking;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public class BookingServiceTests
{
    private readonly BookingService _bookingService;

    public BookingServiceTests()
    {
        // Khởi tạo DbContext InMemory cho testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ApplicationDbContext(options, new HttpContextAccessor());
        _bookingService = new BookingService(context);
    }

    [Fact]
    public void CalculateTotals_EmptyList_ReturnsZeroDurationAndZeroPrice()
    {
        // Arrange
        var services = new List<Service>();

        // Act
        var result = _bookingService.CalculateTotals(services);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalDurationMinutes);
        Assert.Equal(0m, result.TotalPrice);
        Assert.Equal("0 min", result.FormattedTotalDuration);
        Assert.Equal("0 VND", result.FormattedTotalPrice);
        Assert.Empty(result.SelectedServices);
        Assert.Equal("This is an estimate. The salon confirms the final price at checkout", result.PriceNote);
    }

    [Fact]
    public void CalculateTotals_NullList_ReturnsZeroDurationAndZeroPrice()
    {
        // Act
        var result = _bookingService.CalculateTotals(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalDurationMinutes);
        Assert.Equal(0m, result.TotalPrice);
        Assert.Equal("0 min", result.FormattedTotalDuration);
        Assert.Equal("0 VND", result.FormattedTotalPrice);
        Assert.Empty(result.SelectedServices);
    }

    [Fact]
    public void CalculateTotals_SingleService_ReturnsExactDurationAndPrice()
    {
        // Arrange
        var service = new Service
        {
            ServiceId = 1,
            ServiceName = "Cắt tóc nam Classic",
            Price = 150000m,
            DurationMinutes = 45,
            IsActive = true
        };

        // Act
        var result = _bookingService.CalculateTotals(new[] { service });

        // Assert
        Assert.Equal(45, result.TotalDurationMinutes);
        Assert.Equal(150000m, result.TotalPrice);
        Assert.Equal("45 min", result.FormattedTotalDuration);
        Assert.Equal("150.000 VND", result.FormattedTotalPrice);
        Assert.Single(result.SelectedServices);
        Assert.Equal("Cắt tóc nam Classic", result.SelectedServices[0].ServiceName);
        Assert.Equal(150000m, result.SelectedServices[0].Price);
        Assert.Equal(45, result.SelectedServices[0].DurationMinutes);
    }

    [Fact]
    public void CalculateTotals_MultipleServices_ReturnsCorrectCumulativeTotals()
    {
        // Arrange
        var services = new List<Service>
        {
            new() { ServiceId = 1, ServiceName = "Cắt tóc", Price = 180000m, DurationMinutes = 45 },
            new() { ServiceId = 2, ServiceName = "Gội đầu dưỡng sinh", Price = 120000m, DurationMinutes = 30 },
            new() { ServiceId = 3, ServiceName = "Uốn tóc cao cấp", Price = 500000m, DurationMinutes = 90 }
        };

        // Act
        var result = _bookingService.CalculateTotals(services);

        // Assert
        // Total duration: 45 + 30 + 90 = 165 min (2 hr 45 min)
        Assert.Equal(165, result.TotalDurationMinutes);
        Assert.Equal("2 hr 45 min (165 min)", result.FormattedTotalDuration);

        // Tổng tiền: 180.000 + 120.000 + 500.000 = 800.000 VND
        Assert.Equal(800000m, result.TotalPrice);
        Assert.Equal("800.000 VND", result.FormattedTotalPrice);

        Assert.Equal(3, result.SelectedServices.Count);
        Assert.Equal("This is an estimate. The salon confirms the final price at checkout", result.PriceNote);
    }

    [Fact]
    public void CalculateTotals_RemovingService_CorrectlyDeductsDurationAndPrice()
    {
        // Arrange
        var service1 = new Service { ServiceId = 1, ServiceName = "Cắt tạo kiểu", Price = 200000m, DurationMinutes = 40 };
        var service2 = new Service { ServiceId = 2, ServiceName = "Nhuộm thời trang", Price = 650000m, DurationMinutes = 80 };
        var service3 = new Service { ServiceId = 3, ServiceName = "Massage cổ vai gáy", Price = 150000m, DurationMinutes = 20 };

        var initialList = new List<Service> { service1, service2, service3 };
        var initialTotals = _bookingService.CalculateTotals(initialList);
        Assert.Equal(140, initialTotals.TotalDurationMinutes);
        Assert.Equal(1000000m, initialTotals.TotalPrice);

        // Act: Bỏ chọn service2
        initialList.Remove(service2);
        var updatedTotals = _bookingService.CalculateTotals(initialList);

        // Assert: Tổng mới phải bị trừ đúng phần của service2
        Assert.Equal(60, updatedTotals.TotalDurationMinutes);
        Assert.Equal(350000m, updatedTotals.TotalPrice);
        Assert.Equal("1 hr (60 min)", updatedTotals.FormattedTotalDuration);
        Assert.Equal("350.000 VND", updatedTotals.FormattedTotalPrice);
        Assert.Equal(2, updatedTotals.SelectedServices.Count);
        Assert.DoesNotContain(updatedTotals.SelectedServices, s => s.ServiceId == 2);
    }

    [Fact]
    public void CalculateTotals_ServicesWithOddPricesAndDifferentDurations_CalculatesAccurately()
    {
        // Arrange
        var services = new List<Service>
        {
            new() { ServiceId = 1, ServiceName = "Dịch vụ A", Price = 123456m, DurationMinutes = 17 },
            new() { ServiceId = 2, ServiceName = "Dịch vụ B", Price = 87654m, DurationMinutes = 33 },
            new() { ServiceId = 3, ServiceName = "Dịch vụ C", Price = 99999m, DurationMinutes = 71 }
        };

        // Act
        var result = _bookingService.CalculateTotals(services);

        // Assert
        // 17 + 33 + 71 = 121 min (2 hr 1 min)
        Assert.Equal(121, result.TotalDurationMinutes);
        Assert.Equal("2 hr 1 min (121 min)", result.FormattedTotalDuration);

        // 123.456 + 87.654 + 99.999 = 311.109 VND
        Assert.Equal(311109m, result.TotalPrice);
        Assert.Equal("311.109 VND", result.FormattedTotalPrice);
    }

    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(15, "15 min")]
    [InlineData(45, "45 min")]
    [InlineData(60, "1 hr (60 min)")]
    [InlineData(75, "1 hr 15 min (75 min)")]
    [InlineData(120, "2 hr (120 min)")]
    [InlineData(135, "2 hr 15 min (135 min)")]
    public void FormatDuration_VariousMinutes_FormatsCorrectly(int minutes, string expected)
    {
        var result = BookingService.FormatDuration(minutes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CalculateTotalsAsync_OnlyIncludesActiveServices()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        context.Services.AddRange(
            new Service { ServiceId = 10, ServiceName = "Dịch vụ còn bán", Price = 250000m, DurationMinutes = 45, IsActive = true },
            new Service { ServiceId = 20, ServiceName = "Dịch vụ đã ngừng bán", Price = 999000m, DurationMinutes = 120, IsActive = false }
        );
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Act: Chọn cả dịch vụ active và inactive
        var result = await service.CalculateTotalsAsync(new[] { 10, 20 });

        // Assert: Chỉ tính dịch vụ active (Id = 10)
        Assert.Equal(45, result.TotalDurationMinutes);
        Assert.Equal(250000m, result.TotalPrice);
        Assert.Single(result.SelectedServices);
        Assert.Equal(10, result.SelectedServices[0].ServiceId);
    }

    [Fact]
    public async Task BookingController_CalculateTotals_ReturnsOkWithAccurateTotals()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        context.Services.AddRange(
            new Service { ServiceId = 101, ServiceName = "Dịch vụ 1", Price = 100000m, DurationMinutes = 30, IsActive = true },
            new Service { ServiceId = 102, ServiceName = "Dịch vụ 2", Price = 250000m, DurationMinutes = 60, IsActive = true }
        );
        await context.SaveChangesAsync();

        var bookingService = new BookingService(context);
        var controller = new BookingController(bookingService);

        // Act
        var actionResult = await controller.CalculateTotals(new CalculateBookingTotalsRequest
        {
            ServiceIds = new List<int> { 101, 102 }
        });

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var totals = Assert.IsType<BookingTotalsDto>(okResult.Value);

        Assert.Equal(90, totals.TotalDurationMinutes);
        Assert.Equal(350000m, totals.TotalPrice);
        Assert.Equal("1 hr 30 min (90 min)", totals.FormattedTotalDuration);
        Assert.Equal("350.000 VND", totals.FormattedTotalPrice);
        Assert.Equal(2, totals.SelectedServices.Count);
    }
}
