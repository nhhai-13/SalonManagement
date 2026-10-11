using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class ScheduleTransferProcessor(ApplicationDbContext dbContext)
{
    private static readonly string[] TransferableStatuses = ["Pending", "Confirmed", "Arrived"];
    private static readonly string[] NonBlockingStatuses = ["Completed", "Cancelled", "Canceled", "NoShow", "No Show"];

    public async Task<ScheduleTransferBatchResponse> PreviewAsync(
        ScheduleTransferBatchRequest request,
        CancellationToken cancellationToken = default) =>
        await ProcessBatchAsync(request, persist: false, cancellationToken);

    public async Task<ScheduleTransferBatchResponse> TransferAsync(
        ScheduleTransferBatchRequest request,
        CancellationToken cancellationToken = default) =>
        await ProcessBatchAsync(request, persist: true, cancellationToken);

    private async Task<ScheduleTransferBatchResponse> ProcessBatchAsync(
        ScheduleTransferBatchRequest request,
        bool persist,
        CancellationToken cancellationToken)
    {
        var results = new List<ScheduleTransferItemResult>();
        var reservations = new List<ProposedReservation>();
        var seenAppointmentIds = new HashSet<int>();

        foreach (var selection in request.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seenAppointmentIds.Add(selection.AppointmentId))
            {
                results.Add(Failure(selection.AppointmentId,
                    "Yêu cầu chứa cùng một lịch hẹn nhiều lần; hãy tải lại danh sách trước khi xác nhận."));
                continue;
            }

            var result = await ProcessItemAsync(selection, persist, reservations, cancellationToken);
            results.Add(result);
        }

        var successOutcomes = persist
            ? new[] { "Succeeded", "AlreadyApplied" }
            : new[] { "Ready", "AlreadyApplied" };
        var successCount = results.Count(result => successOutcomes.Contains(result.Outcome));
        return new ScheduleTransferBatchResponse(
            results.Count,
            successCount,
            results.Count - successCount,
            results);
    }

    private async Task<ScheduleTransferItemResult> ProcessItemAsync(
        ScheduleTransferSelection selection,
        bool persist,
        List<ProposedReservation> reservations,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;
        EntityEntry<Appointment>? updateEntry = null;
        try
        {
            if (selection.AppointmentId <= 0)
            {
                return Failure(selection.AppointmentId, "Mã lịch hẹn không hợp lệ.");
            }

            if (selection.StylistId <= 0)
            {
                return Failure(selection.AppointmentId, "Thợ thay thế không hợp lệ.");
            }

            if (selection.StartMinuteOfDay is < 0 or >= 1440)
            {
                return Failure(selection.AppointmentId, "Giờ bắt đầu không hợp lệ.");
            }

            if (persist && dbContext.Database.IsRelational())
            {
                // Serialize the final overlap check and update so competing batches cannot
                // both claim the same stylist and time on SQL Server.
                transaction = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
            }

            var appointment = await dbContext.Appointments
                .AsNoTracking()
                .Include(item => item.Stylist)
                .Include(item => item.AppointmentServices)
                    .ThenInclude(item => item.Service)
                        .ThenInclude(service => service.ServiceGroup)
                .SingleOrDefaultAsync(item => item.AppointmentId == selection.AppointmentId, cancellationToken);
            if (appointment is null)
            {
                return Failure(selection.AppointmentId, "Không tìm thấy lịch hẹn; hãy tải lại danh sách.");
            }

            var durationMinutes = ScheduleCoordinationPlanner.GetDuration(appointment);
            if (durationMinutes <= 0)
            {
                return Failure(selection.AppointmentId, "Không xác định được thời lượng dịch vụ của lịch.");
            }

            var start = TimeSpan.FromMinutes(selection.StartMinuteOfDay);
            var end = start + TimeSpan.FromMinutes(durationMinutes);
            if (end > TimeSpan.FromDays(1))
            {
                return Failure(selection.AppointmentId, "Phương án vượt quá cuối ngày; hãy chọn giờ khác.");
            }

            if (appointment.StylistId == selection.StylistId &&
                appointment.StartTime == start && appointment.EndTime == end &&
                string.Equals(appointment.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            {
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return new ScheduleTransferItemResult(selection.AppointmentId, "AlreadyApplied",
                    "Phương án này đã được áp dụng trước đó.", appointment.StylistId,
                    appointment.Stylist.FullName, FormatTime(appointment.StartTime), appointment.Status);
            }

            if (!TransferableStatuses.Contains(appointment.Status, StringComparer.OrdinalIgnoreCase))
            {
                return Failure(selection.AppointmentId,
                    $"Trạng thái hiện tại ({appointment.Status}) không còn cho phép chuyển lịch.");
            }

            if (!selection.CustomerConfirmed)
            {
                return Failure(selection.AppointmentId, "Cần xác nhận khách đã đồng ý phương án mới.");
            }

            if (selection.StylistId == appointment.StylistId)
            {
                return Failure(selection.AppointmentId, "Hãy chọn một thợ khác với thợ đang phụ trách.");
            }

            var date = DateOnly.FromDateTime(appointment.AppointmentDate);
            var dayStart = date.ToDateTime(TimeOnly.MinValue);
            var nextDay = dayStart.AddDays(1);
            var stylist = await dbContext.Stylists
                .AsNoTracking()
                .Include(item => item.WorkSchedules)
                .SingleOrDefaultAsync(item => item.StylistId == selection.StylistId, cancellationToken);
            if (stylist is null || !stylist.IsActive)
            {
                return Failure(selection.AppointmentId, "Thợ thay thế không còn hoạt động.");
            }

            var skillReasons = ScheduleCoordinationPlanner.GetSkillRejectionReasons(stylist, appointment);
            if (skillReasons.Count > 0)
            {
                return Failure(selection.AppointmentId, string.Join(" ", skillReasons));
            }

            var shiftCovers = stylist.WorkSchedules.Any(schedule =>
                DateOnly.FromDateTime(schedule.WorkDate) == date &&
                string.Equals(schedule.Status, "Working", StringComparison.OrdinalIgnoreCase) &&
                schedule.StartTime <= start && schedule.EndTime >= end);
            if (!shiftCovers)
            {
                return Failure(selection.AppointmentId, "Thợ không có ca làm bao phủ toàn bộ giờ đã chọn.");
            }

            var storedHours = await dbContext.BusinessHours
                .AsNoTracking()
                .SingleOrDefaultAsync(hours => hours.DayOfWeek == date.DayOfWeek, cancellationToken);
            var isClosed = storedHours?.IsClosed ?? false;
            var opensAt = (storedHours?.OpensAt ?? new TimeOnly(8, 0)).ToTimeSpan();
            var closesAt = (storedHours?.ClosesAt ?? new TimeOnly(17, 0)).ToTimeSpan();
            if (isClosed || start < opensAt || end > closesAt)
            {
                return Failure(selection.AppointmentId, "Giờ đã chọn nằm ngoài giờ mở cửa của salon.");
            }

            var databaseConflict = await dbContext.Appointments
                .AsNoTracking()
                .Where(item => item.StylistId == stylist.StylistId &&
                    item.AppointmentId != appointment.AppointmentId &&
                    item.AppointmentDate >= dayStart && item.AppointmentDate < nextDay &&
                    !NonBlockingStatuses.Contains(item.Status) &&
                    item.StartTime < end && item.EndTime > start)
                .Select(item => (int?)item.AppointmentId)
                .FirstOrDefaultAsync(cancellationToken);
            if (databaseConflict.HasValue)
            {
                return Failure(selection.AppointmentId,
                    $"Khung giờ vừa bị chiếm bởi lịch hẹn #{databaseConflict.Value}; hãy chọn phương án khác.");
            }

            var batchConflict = reservations.FirstOrDefault(reservation =>
                reservation.AppointmentId != appointment.AppointmentId &&
                reservation.StylistId == stylist.StylistId &&
                reservation.StartMinute < end.TotalMinutes &&
                reservation.EndMinute > start.TotalMinutes);
            if (batchConflict is not null)
            {
                return Failure(selection.AppointmentId,
                    $"Phương án trùng với lịch #{batchConflict.AppointmentId} đã chọn trước trong đợt này.");
            }

            if (!persist)
            {
                reservations.Add(new ProposedReservation(
                    appointment.AppointmentId,
                    stylist.StylistId,
                    (int)start.TotalMinutes,
                    (int)end.TotalMinutes));
                return new ScheduleTransferItemResult(selection.AppointmentId, "Ready",
                    "Phương án hợp lệ tại thời điểm kiểm tra.", stylist.StylistId,
                    stylist.FullName, FormatTime(start), "Confirmed");
            }

            var appointmentEntry = dbContext.ChangeTracker.Entries<Appointment>()
                .FirstOrDefault(entry => entry.Entity.AppointmentId == appointment.AppointmentId);
            if (appointmentEntry is null)
            {
                appointmentEntry = dbContext.Appointments.Attach(new Appointment
                {
                    AppointmentId = appointment.AppointmentId
                });
            }

            appointmentEntry.Entity.StylistId = stylist.StylistId;
            appointmentEntry.Entity.StartTime = start;
            appointmentEntry.Entity.EndTime = end;
            appointmentEntry.Entity.Status = "Confirmed";
            appointmentEntry.Entity.UpdatedAt = DateTime.Now;
            updateEntry = appointmentEntry;
            appointmentEntry.Property(item => item.StylistId).IsModified = true;
            appointmentEntry.Property(item => item.StartTime).IsModified = true;
            appointmentEntry.Property(item => item.EndTime).IsModified = true;
            appointmentEntry.Property(item => item.Status).IsModified = true;
            appointmentEntry.Property(item => item.UpdatedAt).IsModified = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new ScheduleTransferItemResult(selection.AppointmentId, "Succeeded",
                "Đã chuyển lịch thành công.", stylist.StylistId, stylist.FullName, FormatTime(start), "Confirmed");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            if (updateEntry is not null)
            {
                updateEntry.State = EntityState.Detached;
            }

            if (transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    // Preserve the per-item result even if the provider already rolled
                    // back or closed the failed transaction.
                }
            }

            return Failure(selection.AppointmentId,
                "Không thể lưu lịch do dữ liệu vừa thay đổi hoặc lỗi hệ thống; hãy tải lại và thử lại.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static ScheduleTransferItemResult Failure(int appointmentId, string message) =>
        new(appointmentId, "Failed", message, null, null, null, null);

    private static string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm");

    private sealed record ProposedReservation(
        int AppointmentId,
        int StylistId,
        int StartMinute,
        int EndMinute);
}
