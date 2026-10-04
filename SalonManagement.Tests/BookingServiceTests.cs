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
        Assert.Equal("0 phút", result.FormattedTotalDuration);
        Assert.Equal("0 đ", result.FormattedTotalPrice);
        Assert.Empty(result.SelectedServices);
        Assert.Equal("Giá trên là giá tạm tính, giá cuối do tiệm chốt khi thanh toán", result.PriceNote);
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
        Assert.Equal("0 phút", result.FormattedTotalDuration);
        Assert.Equal("0 đ", result.FormattedTotalPrice);
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
        Assert.Equal("45 phút", result.FormattedTotalDuration);
        Assert.Equal("150.000 đ", result.FormattedTotalPrice);
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
        // Tổng thời lượng: 45 + 30 + 90 = 165 phút (2 giờ 45 phút)
        Assert.Equal(165, result.TotalDurationMinutes);
        Assert.Equal("2 giờ 45 phút (165 phút)", result.FormattedTotalDuration);

        // Tổng tiền: 180.000 + 120.000 + 500.000 = 800.000 đ
        Assert.Equal(800000m, result.TotalPrice);
        Assert.Equal("800.000 đ", result.FormattedTotalPrice);

        Assert.Equal(3, result.SelectedServices.Count);
        Assert.Equal("Giá trên là giá tạm tính, giá cuối do tiệm chốt khi thanh toán", result.PriceNote);
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
        Assert.Equal("1 giờ (60 phút)", updatedTotals.FormattedTotalDuration);
        Assert.Equal("350.000 đ", updatedTotals.FormattedTotalPrice);
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
        // 17 + 33 + 71 = 121 phút (2 giờ 1 phút)
        Assert.Equal(121, result.TotalDurationMinutes);
        Assert.Equal("2 giờ 1 phút (121 phút)", result.FormattedTotalDuration);

        // 123.456 + 87.654 + 99.999 = 311.109 đ
        Assert.Equal(311109m, result.TotalPrice);
        Assert.Equal("311.109 đ", result.FormattedTotalPrice);
    }

    [Theory]
    [InlineData(0, "0 phút")]
    [InlineData(15, "15 phút")]
    [InlineData(45, "45 phút")]
    [InlineData(60, "1 giờ (60 phút)")]
    [InlineData(75, "1 giờ 15 phút (75 phút)")]
    [InlineData(120, "2 giờ (120 phút)")]
    [InlineData(135, "2 giờ 15 phút (135 phút)")]
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
        Assert.Equal("1 giờ 30 phút (90 phút)", totals.FormattedTotalDuration);
        Assert.Equal("350.000 đ", totals.FormattedTotalPrice);
        Assert.Equal(2, totals.SelectedServices.Count);
    }

    [Fact]
    public void CalculateTotals_WithExactly5Services_CalculatesSuccessfully()
    {
        // Arrange
        var services = new List<Service>
        {
            new Service { ServiceId = 1, ServiceName = "DV 1", DurationMinutes = 30, Price = 100000m },
            new Service { ServiceId = 2, ServiceName = "DV 2", DurationMinutes = 45, Price = 150000m },
            new Service { ServiceId = 3, ServiceName = "DV 3", DurationMinutes = 20, Price = 80000m },
            new Service { ServiceId = 4, ServiceName = "DV 4", DurationMinutes = 60, Price = 200000m },
            new Service { ServiceId = 5, ServiceName = "DV 5", DurationMinutes = 15, Price = 70000m },
        };

        // Act
        var result = _bookingService.CalculateTotals(services);

        // Assert: 170 phút = 2 giờ 50 phút, tổng tiền 600.000 đ
        Assert.NotNull(result);
        Assert.Equal(5, result.SelectedServices.Count);
        Assert.Equal(170, result.TotalDurationMinutes);
        Assert.Equal(600000m, result.TotalPrice);
    }

    [Fact]
    public void CalculateTotals_WithMoreThan5Services_ThrowsArgumentException()
    {
        // Arrange: 6 dịch vụ
        var services = Enumerable.Range(1, 6).Select(i => new Service
        {
            ServiceId = i,
            ServiceName = $"DV {i}",
            DurationMinutes = 30,
            Price = 100000m
        }).ToList();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _bookingService.CalculateTotals(services));
        Assert.Contains("5", ex.Message);
    }

    [Fact]
    public void CalculateTotals_From5ServicesReducedTo4_CalculatesSuccessfully()
    {
        // Arrange
        var services = Enumerable.Range(1, 5).Select(i => new Service
        {
            ServiceId = i,
            ServiceName = $"DV {i}",
            DurationMinutes = 30,
            Price = 100000m
        }).ToList();

        // Ban đầu tính 5 dịch vụ
        var result5 = _bookingService.CalculateTotals(services);
        Assert.Equal(5, result5.SelectedServices.Count);

        // Act: Bỏ bớt 1 dịch vụ còn 4
        services.RemoveAt(services.Count - 1);
        var result4 = _bookingService.CalculateTotals(services);

        // Assert: Hợp lệ trở lại
        Assert.Equal(4, result4.SelectedServices.Count);
        Assert.Equal(120, result4.TotalDurationMinutes);
        Assert.Equal(400000m, result4.TotalPrice);
    }

    [Fact]
    public async Task CalculateTotalsAsync_WithMoreThan5Services_ThrowsArgumentException()
    {
        // Arrange
        var serviceIds = new List<int> { 1, 2, 3, 4, 5, 6 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _bookingService.CalculateTotalsAsync(serviceIds));
        Assert.Contains("5", ex.Message);
    }

    [Fact]
    public async Task BookingController_CalculateTotals_WithMoreThan5Services_ReturnsBadRequest()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        var bookingService = new BookingService(context);
        var controller = new BookingController(bookingService);

        // Act: Gửi 6 service IDs
        var result = await controller.CalculateTotals(new CalculateBookingTotalsRequest
        {
            ServiceIds = new List<int> { 1, 2, 3, 4, 5, 6 }
        });

        // Assert: Trả về BadRequest kèm thông báo lỗi
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task BookingController_CalculateTotals_With5Services_ReturnsOk()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        for (int i = 1; i <= 5; i++)
        {
            context.Services.Add(new Service
            {
                ServiceId = i,
                ServiceName = $"DV {i}",
                DurationMinutes = 20,
                Price = 50000m,
                IsActive = true
            });
        }
        await context.SaveChangesAsync();

        var bookingService = new BookingService(context);
        var controller = new BookingController(bookingService);

        // Act: Gửi đúng 5 service IDs
        var result = await controller.CalculateTotals(new CalculateBookingTotalsRequest
        {
            ServiceIds = new List<int> { 1, 2, 3, 4, 5 }
        });

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var totals = Assert.IsType<BookingTotalsDto>(okResult.Value);
        Assert.Equal(5, totals.SelectedServices.Count);
        Assert.Equal(100, totals.TotalDurationMinutes);
        Assert.Equal(250000m, totals.TotalPrice);
    }

    [Fact]
    public async Task BookingController_SelectServices_WithMoreThan5Services_ReturnsViewWithErrorMessage()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        var bookingService = new BookingService(context);
        var controller = new BookingController(bookingService);

        // Act: Submit form với 6 service IDs
        var result = await controller.SelectServices(new List<int> { 1, 2, 3, 4, 5, 6 });

        // Assert: Trả về View với ErrorMessage và ModelState có lỗi
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<BookingSelectServicesViewModel>(viewResult.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotNull(model.ErrorMessage);
        Assert.Contains("5", model.ErrorMessage);
    }

    [Fact]
    public void CalculateTotals_DurationLessThanMaxShift_HasNoWarning()
    {
        // Arrange: 180 phút < 240 phút
        var services = new List<Service>
        {
            new Service { ServiceId = 1, ServiceName = "DV 1", DurationMinutes = 60, Price = 100000m },
            new Service { ServiceId = 2, ServiceName = "DV 2", DurationMinutes = 120, Price = 200000m }
        };

        // Act
        var result = _bookingService.CalculateTotals(services, maxShiftDurationMinutes: 240);

        // Assert
        Assert.False(result.HasExceededShiftWarning);
        Assert.Null(result.ShiftWarningMessage);
        Assert.Equal(240, result.MaxShiftDurationMinutes);
    }

    [Fact]
    public void CalculateTotals_DurationEqualToMaxShift_HasNoWarning()
    {
        // Arrange: Đúng 240 phút = 240 phút
        var services = new List<Service>
        {
            new Service { ServiceId = 1, ServiceName = "DV 1", DurationMinutes = 120, Price = 100000m },
            new Service { ServiceId = 2, ServiceName = "DV 2", DurationMinutes = 120, Price = 200000m }
        };

        // Act
        var result = _bookingService.CalculateTotals(services, maxShiftDurationMinutes: 240);

        // Assert
        Assert.False(result.HasExceededShiftWarning);
        Assert.Null(result.ShiftWarningMessage);
    }

    [Fact]
    public void CalculateTotals_DurationExceedsMaxShiftBy1Minute_HasWarning()
    {
        // Arrange: 241 phút > 240 phút
        var services = new List<Service>
        {
            new Service { ServiceId = 1, ServiceName = "DV 1", DurationMinutes = 120, Price = 100000m },
            new Service { ServiceId = 2, ServiceName = "DV 2", DurationMinutes = 121, Price = 200000m }
        };

        // Act
        var result = _bookingService.CalculateTotals(services, maxShiftDurationMinutes: 240);

        // Assert
        Assert.True(result.HasExceededShiftWarning);
        Assert.NotNull(result.ShiftWarningMessage);
        Assert.Contains("241", result.ShiftWarningMessage);
        Assert.Contains("240", result.ShiftWarningMessage);
        Assert.Contains("tách thành 2 lần hẹn", result.ShiftWarningMessage);
    }

    [Fact]
    public void CalculateTotals_RemoveServiceBringsDurationBelowThreshold_WarningDisappears()
    {
        // Arrange: 3 dịch vụ tổng 250 phút > 240 phút
        var services = new List<Service>
        {
            new Service { ServiceId = 1, ServiceName = "DV 1", DurationMinutes = 100, Price = 100000m },
            new Service { ServiceId = 2, ServiceName = "DV 2", DurationMinutes = 100, Price = 100000m },
            new Service { ServiceId = 3, ServiceName = "DV 3", DurationMinutes = 50, Price = 50000m }
        };

        var resultBefore = _bookingService.CalculateTotals(services, maxShiftDurationMinutes: 240);
        Assert.True(resultBefore.HasExceededShiftWarning);

        // Act: Bỏ dịch vụ 3 (còn 200 phút <= 240 phút)
        services.RemoveAt(2);
        var resultAfter = _bookingService.CalculateTotals(services, maxShiftDurationMinutes: 240);

        // Assert: Cảnh báo biến mất
        Assert.False(resultAfter.HasExceededShiftWarning);
        Assert.Null(resultAfter.ShiftWarningMessage);
        Assert.Equal(200, resultAfter.TotalDurationMinutes);
    }

    [Fact]
    public async Task GetMaxShiftDurationMinutesAsync_WithWorkSchedules_ReturnsLongestShift()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        context.WorkSchedules.AddRange(
            new WorkSchedule
            {
                WorkScheduleId = 1,
                StylistId = 1,
                WorkDate = DateTime.Today,
                StartTime = new TimeSpan(8, 0, 0),
                EndTime = new TimeSpan(12, 0, 0), // 4 giờ = 240 phút
                Status = "Working"
            },
            new WorkSchedule
            {
                WorkScheduleId = 2,
                StylistId = 2,
                WorkDate = DateTime.Today,
                StartTime = new TimeSpan(13, 0, 0),
                EndTime = new TimeSpan(18, 30, 0), // 5.5 giờ = 330 phút (ca dài nhất)
                Status = "Working"
            }
        );
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Act
        var maxShift = await service.GetMaxShiftDurationMinutesAsync();

        // Assert
        Assert.Equal(330, maxShift);
    }

    [Fact]
    public async Task GetMaxShiftDurationMinutesAsync_WithoutWorkSchedules_UsesBusinessHours()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        context.BusinessHours.AddRange(
            new BusinessHour
            {
                BusinessHourId = 1,
                DayOfWeek = DayOfWeek.Monday,
                OpensAt = new TimeOnly(8, 0),
                ClosesAt = new TimeOnly(17, 0), // 9 giờ = 540 phút
                IsClosed = false
            }
        );
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Act
        var maxShift = await service.GetMaxShiftDurationMinutesAsync();

        // Assert
        Assert.Equal(540, maxShift);
    }

    [Fact]
    public async Task GetMaxShiftDurationMinutesAsync_NoData_ReturnsDefault240()
    {
        // Arrange: Db trống
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        var service = new BookingService(context);

        // Act
        var maxShift = await service.GetMaxShiftDurationMinutesAsync();

        // Assert: Giá trị mặc định 240 phút
        Assert.Equal(240, maxShift);
    }

    [Fact]
    public async Task CalculateTotalsAsync_WhenExceedsShift_PopulatesWarningInTotals()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, new HttpContextAccessor());
        // Ca dài nhất = 120 phút
        context.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 1,
            StylistId = 1,
            WorkDate = DateTime.Today,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Status = "Working"
        });

        // 2 dịch vụ tổng 150 phút
        context.Services.AddRange(
            new Service { ServiceId = 201, ServiceName = "DV A", DurationMinutes = 90, Price = 200000m, IsActive = true },
            new Service { ServiceId = 202, ServiceName = "DV B", DurationMinutes = 60, Price = 150000m, IsActive = true }
        );
        await context.SaveChangesAsync();

        var service = new BookingService(context);

        // Act
        var totals = await service.CalculateTotalsAsync(new[] { 201, 202 });

        // Assert
        Assert.Equal(150, totals.TotalDurationMinutes);
        Assert.Equal(120, totals.MaxShiftDurationMinutes);
        Assert.True(totals.HasExceededShiftWarning);
        Assert.NotNull(totals.ShiftWarningMessage);
        Assert.Contains("150", totals.ShiftWarningMessage);
        Assert.Contains("120", totals.ShiftWarningMessage);
    }
}
