using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class BookingService(ApplicationDbContext dbContext, CustomerProfileService customerProfiles)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DayLocks = new(StringComparer.Ordinal);

    public async Task<BookingOptionsResponse> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var services = await dbContext.Services.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.ServiceName)
            .Select(item => new BookingOption(item.ServiceId, item.ServiceName))
            .ToListAsync(cancellationToken);
        var stylists = await dbContext.Stylists.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.FullName)
            .Select(item => new BookingOption(item.StylistId, item.FullName))
            .ToListAsync(cancellationToken);
        return new BookingOptionsResponse(services, stylists);
    }

    public async Task<CreateBookingResponse> CreateAsync(
        CreateBookingRequest request,
        bool confirmedByReception,
        CancellationToken cancellationToken = default,
        string changedByUserId = "",
        string changedByEmail = "")
    {
        if (request.UpdateCustomerProfileName && !confirmedByReception)
            throw new BookingValidationException("Chỉ lễ tân mới có thể xác nhận cập nhật tên hồ sơ khách.");
        var service = await dbContext.Services.SingleOrDefaultAsync(
            item => item.ServiceId == request.ServiceId && item.IsActive, cancellationToken)
            ?? throw new BookingValidationException("Dịch vụ không tồn tại hoặc đã ngừng hoạt động.");
        var stylist = await dbContext.Stylists.SingleOrDefaultAsync(
            item => item.StylistId == request.StylistId && item.IsActive, cancellationToken)
            ?? throw new BookingValidationException("Thợ không tồn tại hoặc đã ngừng hoạt động.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new BookingValidationException("Vui lòng nhập họ tên khách.");
        if (service.DurationMinutes <= 0)
            throw new BookingValidationException("Dịch vụ chưa có thời lượng hợp lệ.");

        var endTime = request.StartTime.Add(TimeSpan.FromMinutes(service.DurationMinutes));
        if (endTime <= request.StartTime)
            throw new BookingValidationException("Thời lượng dịch vụ vượt quá một ngày.");

        var hours = await dbContext.BusinessHours.AsNoTracking()
            .SingleOrDefaultAsync(item => item.DayOfWeek == request.AppointmentDate.DayOfWeek, cancellationToken);
        var opensAt = hours?.OpensAt ?? new TimeOnly(8, 0);
        var closesAt = hours?.ClosesAt ?? new TimeOnly(17, 0);
        if (hours?.IsClosed == true || hours?.OpensAt is null && hours is not null || hours?.ClosesAt is null && hours is not null)
            throw new BookingValidationException("Salon đã đóng cửa vào ngày đã chọn.");
        if (request.StartTime < opensAt || endTime > closesAt)
            throw new BookingValidationException($"Lịch hẹn phải nằm trong giờ mở cửa ({opensAt:HH:mm}–{closesAt:HH:mm}).");

        var requestedDate = request.AppointmentDate.ToDateTime(TimeOnly.MinValue);
        var nextDate = requestedDate.AddDays(1);
        var shifts = await dbContext.WorkSchedules.AsNoTracking()
            .Where(shift => shift.StylistId == stylist.StylistId &&
                shift.WorkDate >= requestedDate && shift.WorkDate < nextDate)
            .Select(shift => new { shift.Status, shift.StartTime, shift.EndTime })
            .ToListAsync(cancellationToken);
        var hasWorkingShift = shifts.Any(shift =>
            string.Equals(shift.Status, "Working", StringComparison.OrdinalIgnoreCase) &&
            shift.StartTime <= request.StartTime.ToTimeSpan() &&
            shift.EndTime >= endTime.ToTimeSpan());
        if (!hasWorkingShift)
            throw new BookingValidationException("Chuyên viên chưa có ca làm bao trùm khung giờ đã chọn.");

        var salonTimeZone = TimeZoneInfo.FindSystemTimeZoneById(BusinessHour.SalonTimeZone);
        var salonNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, salonTimeZone);
        var requestedStart = request.AppointmentDate.ToDateTime(request.StartTime);
        if (requestedStart <= salonNow)
            throw new BookingValidationException("Ngày giờ hẹn phải ở thời điểm sắp tới.");

        var dayLock = DayLocks.GetOrAdd($"{stylist.StylistId}:{request.AppointmentDate:yyyy-MM-dd}", _ => new SemaphoreSlim(1, 1));
        await dayLock.WaitAsync(cancellationToken);
        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null)
                transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var appointmentDate = requestedDate;
            var existingAppointments = await dbContext.Appointments.AsNoTracking()
                .Where(appointment => appointment.StylistId == stylist.StylistId &&
                    appointment.AppointmentDate == appointmentDate &&
                    ScheduleCoordinationStatuses.NeedsReview.Contains(appointment.Status))
                .Select(appointment => new { appointment.StartTime, appointment.EndTime })
                .ToListAsync(cancellationToken);
            var overlaps = existingAppointments.Any(appointment =>
                appointment.StartTime < endTime.ToTimeSpan() &&
                appointment.EndTime > request.StartTime.ToTimeSpan());
            if (overlaps)
                throw new BookingConflictException("Khung giờ này vừa được đặt. Vui lòng chọn giờ khác.");

            var resolution = await customerProfiles.ResolveForBookingAsync(
                new ResolveBookingCustomerRequest(request.FullName, request.Phone, null), cancellationToken);
            var appointment = new Appointment
            {
                CustomerId = await FindCustomerIdAsync(request.Phone, cancellationToken),
                CustomerName = request.FullName.Trim(),
                StylistId = stylist.StylistId,
                AppointmentDate = appointmentDate,
                StartTime = request.StartTime.ToTimeSpan(),
                EndTime = endTime.ToTimeSpan(),
                Status = confirmedByReception ? "Confirmed" : "Pending",
                CreatedAt = DateTime.UtcNow,
                AppointmentServices =
                {
                    new AppointmentService
                    {
                        ServiceId = service.ServiceId,
                        Price = service.Price,
                        DurationMinutes = service.DurationMinutes
                    }
                }
            };
            dbContext.Appointments.Add(appointment);

            if (resolution.IsReturningCustomer && request.UpdateCustomerProfileName)
            {
                if (string.IsNullOrWhiteSpace(changedByUserId) || string.IsNullOrWhiteSpace(changedByEmail))
                    throw new BookingValidationException("Không xác định được lễ tân xác nhận cập nhật tên hồ sơ.");

                var customer = await dbContext.Customers.SingleAsync(
                    item => item.CustomerId == appointment.CustomerId, cancellationToken);
                var newName = request.FullName.Trim();
                if (!CustomerProfileService.AreEquivalentNames(customer.FullName, newName))
                {
                    var changedAt = DateTime.UtcNow;
                    dbContext.CustomerProfileChangeLogs.Add(new CustomerProfileChangeLog
                    {
                        CustomerId = customer.CustomerId,
                        ChangedByUserId = changedByUserId,
                        ChangedByEmail = changedByEmail.Trim(),
                        PreviousFullName = customer.FullName,
                        PreviousEmail = customer.Email,
                        PreviousNotes = customer.Notes,
                        UpdatedFullName = newName,
                        UpdatedEmail = customer.Email,
                        UpdatedNotes = customer.Notes,
                        ChangedAtUtc = changedAt
                    });
                    customer.FullName = newName;
                    customer.UpdatedAt = changedAt;
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            return new CreateBookingResponse(
                appointment.AppointmentId,
                appointment.CustomerId,
                resolution.IsReturningCustomer,
                appointment.Status,
                request.AppointmentDate,
                request.StartTime,
                TimeOnly.FromTimeSpan(endTime.ToTimeSpan()),
                service.ServiceName,
                stylist.FullName);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
            dayLock.Release();
        }
    }

    private async Task<int> FindCustomerIdAsync(string phone, CancellationToken cancellationToken)
    {
        var normalizedPhone = CustomerProfileService.NormalizePhone(phone);
        return await dbContext.Customers
            .Where(customer => customer.NormalizedPhone == normalizedPhone)
            .Select(customer => customer.CustomerId)
            .SingleAsync(cancellationToken);
    }
}
