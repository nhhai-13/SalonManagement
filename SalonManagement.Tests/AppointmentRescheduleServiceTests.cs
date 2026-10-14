using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Reception;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Appointments;
using SalonManagement.Services.Appointments;
using Xunit;

namespace SalonManagement.Tests;

public class AppointmentRescheduleServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new HttpContextAccessor());
    }

    [Fact]
    public async Task ValidateReschedule_WhenStylistLacksSkill_ReturnsF33Error()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var service1 = new Service { ServiceId = 1, ServiceName = "Cắt tóc nam", DurationMinutes = 30, Price = 100000m, IsActive = true };
        var service2 = new Service { ServiceId = 2, ServiceName = "Uốn tóc", DurationMinutes = 60, Price = 300000m, IsActive = true };
        db.Services.AddRange(service1, service2);

        var stylistA = new Stylist { StylistId = 1, FullName = "Nguyễn Văn Thợ A", IsActive = true };
        var stylistB = new Stylist { StylistId = 2, FullName = "Trần Văn Thợ B", IsActive = true };
        db.Stylists.AddRange(stylistA, stylistB);

        // Thợ A làm được cả 2 dịch vụ
        db.StylistServices.AddRange(
            new StylistService { StylistId = 1, ServiceId = 1 },
            new StylistService { StylistId = 1, ServiceId = 2 }
        );

        // Thợ B CHỈ làm được Cắt tóc nam (thiếu Uốn tóc)
        db.StylistServices.Add(new StylistService { StylistId = 2, ServiceId = 1 });

        var customer = new Customer { CustomerId = 1, FullName = "Khách Hàng A", Phone = "0901234567" };
        db.Customers.Add(customer);

        var date = DateTime.Today.AddDays(1);
        var appointment = new Appointment
        {
            AppointmentId = 10,
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 30, 0),
            Status = "Confirmed"
        };
        db.Appointments.Add(appointment);

        db.AppointmentServices.AddRange(
            new AppointmentService { AppointmentServiceId = 1, AppointmentId = 10, ServiceId = 1, DurationMinutes = 30, Price = 100000m },
            new AppointmentService { AppointmentServiceId = 2, AppointmentId = 10, ServiceId = 2, DurationMinutes = 60, Price = 300000m }
        );

        // Ca làm của thợ B
        db.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 1,
            StylistId = 2,
            WorkDate = date,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(17, 0, 0),
            Status = "Working"
        });

        await db.SaveChangesAsync();

        var service = new AppointmentRescheduleService(db);

        // Act: Đổi sang Thợ B (thiếu dịch vụ Uốn tóc)
        var result = await service.ValidateRescheduleAsync(10, 2, date, new TimeSpan(9, 0, 0));

        // Assert (AC2: Lỗi F33)
        Assert.False(result.IsValid);
        Assert.Equal("F33", result.ErrorCode);
        Assert.Contains("Uốn tóc", result.ErrorMessage);
        Assert.Contains("Nguyễn Văn Thợ A" != null ? "Trần Văn Thợ B" : "", result.ErrorMessage);
        Assert.Single(result.UnqualifiedServices);
        Assert.Equal("Uốn tóc", result.UnqualifiedServices[0]);
    }

    [Fact]
    public async Task ValidateReschedule_WhenStylistHasAllSkills_PassesSkillValidation()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var svc = new Service { ServiceId = 1, ServiceName = "Gội đầu dưỡng sinh", DurationMinutes = 45, Price = 150000m, IsActive = true };
        db.Services.Add(svc);

        var stylist = new Stylist { StylistId = 1, FullName = "Lê Thị Thợ", IsActive = true };
        db.Stylists.Add(stylist);
        db.StylistServices.Add(new StylistService { StylistId = 1, ServiceId = 1 });

        var customer = new Customer { CustomerId = 1, FullName = "Khách Hàng B", Phone = "0912345678" };
        db.Customers.Add(customer);

        var date = DateTime.Today.AddDays(1);
        var appt = new Appointment
        {
            AppointmentId = 20,
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = date,
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(10, 45, 0),
            Status = "Confirmed"
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(new AppointmentService { AppointmentServiceId = 1, AppointmentId = 20, ServiceId = 1, DurationMinutes = 45, Price = 150000m });

        db.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 1,
            StylistId = 1,
            WorkDate = date,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Status = "Working"
        });

        await db.SaveChangesAsync();

        var service = new AppointmentRescheduleService(db);

        // Act
        var result = await service.ValidateRescheduleAsync(20, 1, date, new TimeSpan(14, 0, 0));

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task ValidateReschedule_WhenNewTimeSlotIsOutsideShift_ReturnsOutOfShiftError()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var svc = new Service { ServiceId = 1, ServiceName = "Cắt tóc", DurationMinutes = 60, Price = 100000m, IsActive = true };
        db.Services.Add(svc);

        var stylist = new Stylist { StylistId = 1, FullName = "Thợ Ca Sáng", IsActive = true };
        db.Stylists.Add(stylist);
        db.StylistServices.Add(new StylistService { StylistId = 1, ServiceId = 1 });

        var date = DateTime.Today.AddDays(1);
        // Ca sáng: 08:00 - 12:00
        db.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 1,
            StylistId = 1,
            WorkDate = date,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(12, 0, 0),
            Status = "Working"
        });

        var appt = new Appointment
        {
            AppointmentId = 30,
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Status = "Pending"
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(new AppointmentService { AppointmentServiceId = 1, AppointmentId = 30, ServiceId = 1, DurationMinutes = 60, Price = 100000m });

        await db.SaveChangesAsync();

        var service = new AppointmentRescheduleService(db);

        // Act 1: Dời sang 13:00 - 14:00 (hoàn toàn ngoài ca)
        var resultAfternoon = await service.ValidateRescheduleAsync(30, 1, date, new TimeSpan(13, 0, 0));

        // Assert 1
        Assert.False(resultAfternoon.IsValid);
        Assert.Equal("OUT_OF_SHIFT", resultAfternoon.ErrorCode);

        // Act 2: Dời sang 11:30 - 12:30 (bắt đầu trong ca nhưng kết thúc lẹm ra ngoài ca)
        var resultPartial = await service.ValidateRescheduleAsync(30, 1, date, new TimeSpan(11, 30, 0));

        // Assert 2
        Assert.False(resultPartial.IsValid);
        Assert.Equal("OUT_OF_SHIFT", resultPartial.ErrorCode);
    }

    [Fact]
    public async Task ValidateReschedule_WhenSlotOverlapsAnotherAppointment_ReturnsOverlapError()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var svc = new Service { ServiceId = 1, ServiceName = "Nhuộm tóc", DurationMinutes = 60, Price = 500000m, IsActive = true };
        db.Services.Add(svc);

        var stylist = new Stylist { StylistId = 1, FullName = "Thợ A", IsActive = true };
        db.Stylists.Add(stylist);
        db.StylistServices.Add(new StylistService { StylistId = 1, ServiceId = 1 });

        var date = DateTime.Today.AddDays(1);
        db.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 1,
            StylistId = 1,
            WorkDate = date,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Status = "Working"
        });

        // Lịch hẹn 1 của khách C: 09:00 - 10:00
        var cust1 = new Customer { CustomerId = 1, FullName = "Khách Đã Đặt Trước" };
        var cust2 = new Customer { CustomerId = 2, FullName = "Khách Cần Dời" };
        db.Customers.AddRange(cust1, cust2);

        var apptExisting = new Appointment
        {
            AppointmentId = 41,
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Status = "Confirmed"
        };

        // Lịch hẹn 2 cần dời: ban đầu lúc 14:00 - 15:00
        var apptToReschedule = new Appointment
        {
            AppointmentId = 42,
            CustomerId = 2,
            StylistId = 1,
            AppointmentDate = date,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            Status = "Confirmed"
        };

        db.Appointments.AddRange(apptExisting, apptToReschedule);
        db.AppointmentServices.Add(new AppointmentService { AppointmentServiceId = 1, AppointmentId = 42, ServiceId = 1, DurationMinutes = 60, Price = 500000m });

        await db.SaveChangesAsync();

        var service = new AppointmentRescheduleService(db);

        // Act: Thử dời Lịch hẹn 2 sang 09:30 - 10:30 (chồng lấn với lịch hẹn 1 lúc 09:00-10:00)
        var result = await service.ValidateRescheduleAsync(42, 1, date, new TimeSpan(9, 30, 0));

        // Assert (AC3: Trùng lấn)
        Assert.False(result.IsValid);
        Assert.Equal("OVERLAP", result.ErrorCode);
        Assert.NotNull(result.ConflictingStartTime);
        Assert.NotNull(result.ConflictingEndTime);
        Assert.Equal(new TimeSpan(9, 0, 0), result.ConflictingStartTime);
        Assert.Equal(new TimeSpan(10, 0, 0), result.ConflictingEndTime);
        Assert.Contains("Khách Đã Đặt Trước", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateReschedule_WhenSlotTouchesBoundary_IsAllowedWithoutOverlap()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var svc = new Service { ServiceId = 1, ServiceName = "Dịch vụ 60p", DurationMinutes = 60, Price = 100000m, IsActive = true };
        db.Services.Add(svc);

        var stylist = new Stylist { StylistId = 1, FullName = "Thợ Chuẩn", IsActive = true };
        db.Stylists.Add(stylist);
        db.StylistServices.Add(new StylistService { StylistId = 1, ServiceId = 1 });

        var date = DateTime.Today.AddDays(1);
        db.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 1,
            StylistId = 1,
            WorkDate = date,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Status = "Working"
        });

        // Lịch hẹn hiện có: 09:00 - 10:00
        var existing = new Appointment
        {
            AppointmentId = 51,
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = date,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(10, 0, 0),
            Status = "Confirmed"
        };

        // Lịch hẹn cần dời: 60 phút
        var toMove = new Appointment
        {
            AppointmentId = 52,
            CustomerId = 2,
            StylistId = 1,
            AppointmentDate = date,
            StartTime = new TimeSpan(14, 0, 0),
            EndTime = new TimeSpan(15, 0, 0),
            Status = "Confirmed"
        };
        db.Appointments.AddRange(existing, toMove);
        db.AppointmentServices.Add(new AppointmentService { AppointmentServiceId = 1, AppointmentId = 52, ServiceId = 1, DurationMinutes = 60, Price = 100000m });

        await db.SaveChangesAsync();

        var service = new AppointmentRescheduleService(db);

        // Case 1: Chạm mép sau: 10:00 - 11:00 (bắt đầu đúng lúc lịch trước kết thúc) -> Hợp lệ
        var resultAfter = await service.ValidateRescheduleAsync(52, 1, date, new TimeSpan(10, 0, 0));
        Assert.True(resultAfter.IsValid);

        // Case 2: Chạm mép trước: 08:00 - 09:00 (kết thúc đúng lúc lịch sau bắt đầu) -> Hợp lệ
        var resultBefore = await service.ValidateRescheduleAsync(52, 1, date, new TimeSpan(8, 0, 0));
        Assert.True(resultBefore.IsValid);
    }

    [Fact]
    public async Task ValidateReschedule_WhenAppointmentIsCompletedOrCancelled_ReturnsInvalidStatus()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var svc = new Service { ServiceId = 1, ServiceName = "Dịch vụ A", DurationMinutes = 30, Price = 100000m, IsActive = true };
        db.Services.Add(svc);
        var stylist = new Stylist { StylistId = 1, FullName = "Thợ A", IsActive = true };
        db.Stylists.Add(stylist);
        db.StylistServices.Add(new StylistService { StylistId = 1, ServiceId = 1 });

        var date = DateTime.Today;
        db.WorkSchedules.Add(new WorkSchedule { WorkScheduleId = 1, StylistId = 1, WorkDate = date, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(18, 0, 0), Status = "Working" });

        var apptCompleted = new Appointment { AppointmentId = 61, CustomerId = 1, StylistId = 1, AppointmentDate = date, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(9, 30, 0), Status = "Completed" };
        var apptCancelled = new Appointment { AppointmentId = 62, CustomerId = 1, StylistId = 1, AppointmentDate = date, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(10, 30, 0), Status = "Cancelled" };
        db.Appointments.AddRange(apptCompleted, apptCancelled);

        await db.SaveChangesAsync();

        var service = new AppointmentRescheduleService(db);

        // Act & Assert
        var resCompleted = await service.ValidateRescheduleAsync(61, 1, date, new TimeSpan(14, 0, 0));
        Assert.False(resCompleted.IsValid);
        Assert.Equal("INVALID_STATUS", resCompleted.ErrorCode);

        var resCancelled = await service.ValidateRescheduleAsync(62, 1, date, new TimeSpan(14, 0, 0));
        Assert.False(resCancelled.IsValid);
        Assert.Equal("INVALID_STATUS", resCancelled.ErrorCode);
    }

    [Fact]
    public async Task RescheduleAppointment_WhenValid_UpdatesAppointmentSuccessfully()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var svc = new Service { ServiceId = 1, ServiceName = "Gội & Cắt", DurationMinutes = 45, Price = 150000m, IsActive = true };
        db.Services.Add(svc);

        var stylist1 = new Stylist { StylistId = 1, FullName = "Thợ Cũ", IsActive = true };
        var stylist2 = new Stylist { StylistId = 2, FullName = "Thợ Mới", IsActive = true };
        db.Stylists.AddRange(stylist1, stylist2);
        db.StylistServices.AddRange(
            new StylistService { StylistId = 1, ServiceId = 1 },
            new StylistService { StylistId = 2, ServiceId = 1 }
        );

        var dateOld = DateTime.Today.AddDays(1);
        var dateNew = DateTime.Today.AddDays(2);

        db.WorkSchedules.Add(new WorkSchedule
        {
            WorkScheduleId = 1,
            StylistId = 2,
            WorkDate = dateNew,
            StartTime = new TimeSpan(8, 0, 0),
            EndTime = new TimeSpan(18, 0, 0),
            Status = "Working"
        });

        var customer = new Customer { CustomerId = 1, FullName = "Khách Test", Phone = "0901234567" };
        db.Customers.Add(customer);

        var appt = new Appointment
        {
            AppointmentId = 70,
            CustomerId = 1,
            StylistId = 1,
            AppointmentDate = dateOld,
            StartTime = new TimeSpan(9, 0, 0),
            EndTime = new TimeSpan(9, 45, 0),
            Status = "Confirmed"
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(new AppointmentService { AppointmentServiceId = 1, AppointmentId = 70, ServiceId = 1, DurationMinutes = 45, Price = 150000m });

        await db.SaveChangesAsync();

        var service = new AppointmentRescheduleService(db);

        // Act
        var request = new RescheduleAppointmentRequest
        {
            NewStylistId = 2,
            NewDate = dateNew,
            NewStartTime = new TimeSpan(11, 0, 0),
            Reason = "Khách đổi lịch bận buổi sáng"
        };

        var result = await service.RescheduleAppointmentAsync(70, request, "user-receptionist-id", "receptionist@salon.com");

        // Assert
        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.UpdatedAppointment);
        Assert.Equal(2, result.UpdatedAppointment.StylistId);
        Assert.Equal(dateNew.Date, result.UpdatedAppointment.AppointmentDate.Date);
        Assert.Equal(new TimeSpan(11, 0, 0), result.UpdatedAppointment.StartTime);
        Assert.Equal(new TimeSpan(11, 45, 0), result.UpdatedAppointment.EndTime);

        // Kiểm tra database thực tế
        var updatedInDb = await db.Appointments.FindAsync(70);
        Assert.NotNull(updatedInDb);
        Assert.Equal(2, updatedInDb.StylistId);
        Assert.Equal(dateNew.Date, updatedInDb.AppointmentDate.Date);
        Assert.Equal(new TimeSpan(11, 0, 0), updatedInDb.StartTime);
        Assert.Equal(new TimeSpan(11, 45, 0), updatedInDb.EndTime);
        Assert.Contains("Khách đổi lịch", updatedInDb.Notes ?? "");
        Assert.NotNull(updatedInDb.UpdatedAt);
    }

    [Fact]
    public async Task Controller_ValidateAndReschedule_Interactions()
    {
        // Arrange
        await using var db = CreateInMemoryDbContext();

        var svc = new Service { ServiceId = 1, ServiceName = "Dịch vụ cơ bản", DurationMinutes = 30, Price = 100000m, IsActive = true };
        db.Services.Add(svc);
        var stylist = new Stylist { StylistId = 1, FullName = "Thợ 1", IsActive = true };
        db.Stylists.Add(stylist);
        db.StylistServices.Add(new StylistService { StylistId = 1, ServiceId = 1 });

        var date = DateTime.Today.AddDays(1);
        db.WorkSchedules.Add(new WorkSchedule { WorkScheduleId = 1, StylistId = 1, WorkDate = date, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(17, 0, 0), Status = "Working" });

        var customer = new Customer { CustomerId = 1, FullName = "Khách Test 2", Phone = "0901234568" };
        db.Customers.Add(customer);

        var appt = new Appointment { AppointmentId = 80, CustomerId = 1, StylistId = 1, AppointmentDate = date, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(9, 30, 0), Status = "Confirmed" };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(new AppointmentService { AppointmentServiceId = 1, AppointmentId = 80, ServiceId = 1, DurationMinutes = 30, Price = 100000m });
        await db.SaveChangesAsync();

        var service = new AppointmentRescheduleService(db);
        var controller = new ReceptionAppointmentsController(service);

        // Act 1: Gọi endpoint validate hợp lệ
        var validReq = new RescheduleAppointmentRequest { NewStylistId = 1, NewDate = date, NewStartTime = new TimeSpan(10, 0, 0) };
        var actionRes = await controller.ValidateReschedule(80, validReq, default);
        var okVal = Assert.IsType<OkObjectResult>(actionRes);
        var valResult = Assert.IsType<RescheduleValidationResult>(okVal.Value);
        Assert.True(valResult.IsValid);

        // Act 2: Gọi endpoint reschedule
        var resAction = await controller.Reschedule(80, validReq, default);
        var okRes = Assert.IsType<OkObjectResult>(resAction);
        var resDto = Assert.IsType<RescheduleResult>(okRes.Value);
        Assert.True(resDto.Success);
    }
}
