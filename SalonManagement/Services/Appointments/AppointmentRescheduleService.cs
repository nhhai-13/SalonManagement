using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Models.ViewModels.Appointments;

namespace SalonManagement.Services.Appointments;

public class AppointmentRescheduleService : IAppointmentRescheduleService
{
    private readonly ApplicationDbContext _db;

    public AppointmentRescheduleService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DailyScheduleDto> GetDailyScheduleAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;

        // 1. Giờ hoạt động của salon trong ngày
        var businessHour = await _db.BusinessHours.AsNoTracking()
            .FirstOrDefaultAsync(bh => bh.DayOfWeek == targetDate.DayOfWeek, cancellationToken);

        var openTime = businessHour?.OpensAt?.ToTimeSpan() ?? new TimeSpan(8, 0, 0);
        var closeTime = businessHour?.ClosesAt?.ToTimeSpan() ?? new TimeSpan(20, 0, 0);
        var isClosed = businessHour?.IsClosed ?? false;

        // 2. Danh sách thợ đang hoạt động
        var stylists = await _db.Stylists.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.FullName)
            .ToListAsync(cancellationToken);

        var stylistIds = stylists.Select(s => s.StylistId).ToList();

        // 3. Ca làm việc của thợ trong ngày
        var shifts = await _db.WorkSchedules.AsNoTracking()
            .Where(ws => stylistIds.Contains(ws.StylistId) && ws.WorkDate.Date == targetDate && ws.Status == "Working")
            .OrderBy(ws => ws.StartTime)
            .ToListAsync(cancellationToken);

        var shiftsByStylist = shifts.ToLookup(ws => ws.StylistId);

        // 4. Lịch hẹn của salon trong ngày (loại trừ các lịch đã hủy)
        var appointments = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate.Date == targetDate && a.Status != "Cancelled")
            .Include(a => a.Customer)
            .Include(a => a.AppointmentServices)
                .ThenInclude(asvc => asvc.Service)
            .OrderBy(a => a.StartTime)
            .ToListAsync(cancellationToken);

        var appointmentsByStylist = appointments.ToLookup(a => a.StylistId);

        // 5. Tổng hợp cột theo thợ
        var stylistColumns = stylists.Select(stylist =>
        {
            var stylistShifts = shiftsByStylist[stylist.StylistId].Select(ws => new ShiftWindowDto
            {
                StartTime = ws.StartTime,
                EndTime = ws.EndTime
            }).ToList();

            var stylistAppointments = appointmentsByStylist[stylist.StylistId].Select(a =>
            {
                var duration = a.AppointmentServices.Sum(s => s.DurationMinutes);
                var serviceSummary = string.Join(", ", a.AppointmentServices.Select(s => s.Service?.ServiceName ?? "").Where(s => !string.IsNullOrEmpty(s)));

                return new AppointmentCalendarCardDto
                {
                    AppointmentId = a.AppointmentId,
                    CustomerId = a.CustomerId,
                    CustomerName = a.Customer?.FullName ?? "Khách lẻ",
                    CustomerPhone = a.Customer?.Phone ?? string.Empty,
                    StylistId = a.StylistId,
                    StylistName = stylist.FullName,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    DurationMinutes = duration,
                    Status = a.Status,
                    ServiceNamesSummary = serviceSummary,
                    TotalAmount = a.AppointmentServices.Sum(s => s.Price)
                };
            }).ToList();

            return new StylistColumnDto
            {
                StylistId = stylist.StylistId,
                FullName = stylist.FullName,
                Specialty = stylist.Specialty,
                ProfileImagePath = stylist.ProfileImagePath,
                Shifts = stylistShifts,
                Appointments = stylistAppointments
            };
        }).ToList();

        return new DailyScheduleDto
        {
            Date = targetDate,
            FormattedDate = targetDate.ToString("dd/MM/yyyy"),
            SalonOpenTime = openTime,
            SalonCloseTime = closeTime,
            IsClosed = isClosed,
            Stylists = stylistColumns
        };
    }

    public async Task<AppointmentDetailForRescheduleDto?> GetRescheduleInfoAsync(int appointmentId, DateTime? targetDate = null, CancellationToken cancellationToken = default)
    {
        var appointment = await _db.Appointments.AsNoTracking()
            .Include(a => a.Customer)
            .Include(a => a.Stylist)
            .Include(a => a.AppointmentServices)
                .ThenInclude(asvc => asvc.Service)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, cancellationToken);

        if (appointment == null) return null;

        var dateToCheck = targetDate?.Date ?? appointment.AppointmentDate.Date;
        var requiredServiceIds = appointment.AppointmentServices.Select(s => s.ServiceId).Distinct().ToList();
        var totalDuration = appointment.AppointmentServices.Sum(s => s.DurationMinutes);

        // Lấy tất cả thợ đang hoạt động kèm kỹ năng
        var activeStylists = await _db.Stylists.AsNoTracking()
            .Where(s => s.IsActive)
            .Include(s => s.Services)
            .OrderBy(s => s.FullName)
            .ToListAsync(cancellationToken);

        // Lấy ca làm việc của các thợ trong ngày targetDate
        var stylistIds = activeStylists.Select(s => s.StylistId).ToList();
        var shifts = await _db.WorkSchedules.AsNoTracking()
            .Where(ws => stylistIds.Contains(ws.StylistId) && ws.WorkDate.Date == dateToCheck && ws.Status == "Working")
            .OrderBy(ws => ws.StartTime)
            .ToListAsync(cancellationToken);

        var shiftsByStylist = shifts.ToLookup(ws => ws.StylistId);

        // Đánh giá từng thợ
        var eligibleStylists = activeStylists.Select(stylist =>
        {
            var stylistSkillIds = stylist.Services.Select(ss => ss.ServiceId).ToHashSet();
            var missingServices = appointment.AppointmentServices
                .Where(s => !stylistSkillIds.Contains(s.ServiceId))
                .Select(s => s.Service?.ServiceName ?? $"Dịch vụ #{s.ServiceId}")
                .Distinct()
                .ToList();

            var stylistShifts = shiftsByStylist[stylist.StylistId].Select(ws => new ShiftWindowDto
            {
                StartTime = ws.StartTime,
                EndTime = ws.EndTime
            }).ToList();

            return new EligibleStylistDto
            {
                StylistId = stylist.StylistId,
                FullName = stylist.FullName,
                HasAllSkills = missingServices.Count == 0,
                MissingServices = missingServices,
                HasWorkingShift = stylistShifts.Count > 0,
                Shifts = stylistShifts
            };
        }).ToList();

        return new AppointmentDetailForRescheduleDto
        {
            AppointmentId = appointment.AppointmentId,
            CustomerId = appointment.CustomerId,
            CustomerName = appointment.Customer?.FullName ?? "Khách lẻ",
            CustomerPhone = appointment.Customer?.Phone ?? string.Empty,
            CustomerEmail = appointment.Customer?.Email,
            CurrentStylistId = appointment.StylistId,
            CurrentStylistName = appointment.Stylist?.FullName ?? string.Empty,
            CurrentDate = appointment.AppointmentDate,
            CurrentStartTime = appointment.StartTime,
            CurrentEndTime = appointment.EndTime,
            TotalDurationMinutes = totalDuration,
            Status = appointment.Status,
            Services = appointment.AppointmentServices.Select(asvc => new AppointmentServiceItemDto
            {
                ServiceId = asvc.ServiceId,
                ServiceName = asvc.Service?.ServiceName ?? string.Empty,
                Price = asvc.Price,
                DurationMinutes = asvc.DurationMinutes
            }).ToList(),
            EligibleStylists = eligibleStylists
        };
    }

    public async Task<RescheduleValidationResult> ValidateRescheduleAsync(int appointmentId, int newStylistId, DateTime newDate, TimeSpan newStartTime, CancellationToken cancellationToken = default)
    {
        // 1. Kiểm tra tồn tại lịch hẹn
        var appointment = await _db.Appointments.AsNoTracking()
            .Include(a => a.AppointmentServices)
                .ThenInclude(asvc => asvc.Service)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, cancellationToken);

        if (appointment == null)
        {
            return RescheduleValidationResult.Fail("NOT_FOUND", "Không tìm thấy lịch hẹn cần điều chỉnh.");
        }

        // 2. Kiểm tra trạng thái lịch hẹn: Đã hoàn tất hoặc đã hủy thì không được sửa
        if (appointment.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
            appointment.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return RescheduleValidationResult.Fail("INVALID_STATUS", "Lịch hẹn đã hoàn tất hoặc đã hủy, không thể thay đổi thợ hoặc dời giờ hẹn.");
        }

        // 3. Tính toán tổng thời lượng và giờ kết thúc mới
        var totalDuration = appointment.AppointmentServices.Sum(s => s.DurationMinutes);
        if (totalDuration <= 0) totalDuration = 30; // Fallback an toàn tối thiểu
        var newEndTime = newStartTime.Add(TimeSpan.FromMinutes(totalDuration));

        // 4. Kiểm tra thợ mới có tồn tại và đang hoạt động không
        var newStylist = await _db.Stylists.AsNoTracking()
            .Include(s => s.Services)
            .FirstOrDefaultAsync(s => s.StylistId == newStylistId && s.IsActive, cancellationToken);

        if (newStylist == null)
        {
            return RescheduleValidationResult.Fail("STYLIST_NOT_FOUND", "Thợ được chọn không tồn tại hoặc đã ngừng hoạt động.");
        }

        // 5. [AC2 - Lỗi F33]: Kiểm tra kỹ năng thợ mới
        var stylistSkillIds = newStylist.Services.Select(ss => ss.ServiceId).ToHashSet();
        var missingServices = appointment.AppointmentServices
            .Where(asvc => !stylistSkillIds.Contains(asvc.ServiceId))
            .Select(asvc => asvc.Service?.ServiceName ?? $"Dịch vụ #{asvc.ServiceId}")
            .Distinct()
            .ToList();

        if (missingServices.Count > 0)
        {
            return RescheduleValidationResult.FailF33(newStylist.FullName, missingServices);
        }

        // 6. [AC3 - Ngoài ca làm việc]: Kiểm tra khung giờ nằm trong ca làm việc của thợ
        var targetDate = newDate.Date;
        var shifts = await _db.WorkSchedules.AsNoTracking()
            .Where(ws => ws.StylistId == newStylistId && ws.WorkDate.Date == targetDate && ws.Status == "Working")
            .OrderBy(ws => ws.StartTime)
            .ToListAsync(cancellationToken);

        if (shifts.Count == 0)
        {
            return RescheduleValidationResult.FailOutOfShift(newStylist.FullName, $"Thợ không có ca làm việc nào trong ngày {targetDate:dd/MM/yyyy}.");
        }

        // Gộp các ca làm việc liền kề hoặc giao nhau
        var mergedWindows = new List<(TimeSpan Start, TimeSpan End)>();
        foreach (var shift in shifts)
        {
            if (mergedWindows.Count > 0 && shift.StartTime <= mergedWindows[^1].End)
            {
                var last = mergedWindows[^1];
                mergedWindows[^1] = (last.Start, shift.EndTime > last.End ? shift.EndTime : last.End);
            }
            else
            {
                mergedWindows.Add((shift.StartTime, shift.EndTime));
            }
        }

        var isWithinShift = mergedWindows.Any(w => newStartTime >= w.Start && newEndTime <= w.End);
        if (!isWithinShift)
        {
            var shiftDesc = $"Ca làm việc trong ngày: {string.Join(", ", shifts.Select(s => $"{s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm}"))}.";
            return RescheduleValidationResult.FailOutOfShift(newStylist.FullName, shiftDesc);
        }

        // 7. [AC3 - Trùng lấn lịch hẹn]: Khung giờ mới không được chồng lấn lịch khác của thợ
        // Hai khoảng [A, B) và [C, D) trùng nhau khi: A < D && B > C
        // Chạm mép (A == D hoặc B == C) là hoàn toàn hợp lệ
        var existingAppointments = await _db.Appointments.AsNoTracking()
            .Where(a => a.StylistId == newStylistId
                        && a.AppointmentDate.Date == targetDate
                        && a.AppointmentId != appointmentId
                        && a.Status != "Cancelled")
            .Include(a => a.Customer)
            .ToListAsync(cancellationToken);

        var conflicting = existingAppointments.FirstOrDefault(a => newStartTime < a.EndTime && newEndTime > a.StartTime);
        if (conflicting != null)
        {
            return RescheduleValidationResult.FailOverlap(conflicting.StartTime, conflicting.EndTime, conflicting.Customer?.FullName);
        }

        return RescheduleValidationResult.Success();
    }

    public async Task<RescheduleResult> RescheduleAppointmentAsync(int appointmentId, RescheduleAppointmentRequest request, string? currentUserId = null, string? currentUserName = null, CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            IDbContextTransaction? transaction = null;

            if (_db.Database.IsRelational())
            {
                transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            }

            try
            {
                // Kiểm tra validation chặt chẽ bên trong Transaction
                var validation = await ValidateRescheduleAsync(appointmentId, request.NewStylistId, request.NewDate, request.NewStartTime, cancellationToken);
                if (!validation.IsValid)
                {
                    return new RescheduleResult
                    {
                        Success = false,
                        Message = validation.ErrorMessage ?? "Yêu cầu dời lịch không hợp lệ.",
                        Validation = validation
                    };
                }

                var appointment = await _db.Appointments
                    .Include(a => a.Customer)
                    .Include(a => a.Stylist)
                    .Include(a => a.AppointmentServices)
                        .ThenInclude(asvc => asvc.Service)
                    .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, cancellationToken);

                if (appointment == null)
                {
                    return new RescheduleResult
                    {
                        Success = false,
                        Message = "Không tìm thấy lịch hẹn."
                    };
                }

                var totalDuration = appointment.AppointmentServices.Sum(s => s.DurationMinutes);
                if (totalDuration <= 0) totalDuration = 30;

                var oldStylistId = appointment.StylistId;
                var oldStylistName = appointment.Stylist?.FullName ?? (await _db.Stylists.FindAsync(new object[] { oldStylistId }, cancellationToken))?.FullName;
                var oldDate = appointment.AppointmentDate;
                var oldStartTime = appointment.StartTime;
                var oldEndTime = appointment.EndTime;

                var newEndTime = request.NewStartTime.Add(TimeSpan.FromMinutes(totalDuration));
                var newStylist = await _db.Stylists.FindAsync(new object[] { request.NewStylistId }, cancellationToken);
                var newStylistName = newStylist?.FullName ?? string.Empty;

                bool hasChanged = oldStylistId != request.NewStylistId
                    || oldDate.Date != request.NewDate.Date
                    || oldStartTime != request.NewStartTime;

                // [AC4 - Audit Trail]: Ghi nhận nhật ký thay đổi lịch hẹn trong cùng transaction
                if (hasChanged)
                {
                    var changeLog = new AppointmentChangeLog
                    {
                        AppointmentId = appointmentId,
                        ModifiedByUserId = string.IsNullOrWhiteSpace(currentUserId) ? "SYSTEM" : currentUserId,
                        ModifiedByUserName = string.IsNullOrWhiteSpace(currentUserName) ? "Lễ tân" : currentUserName,
                        OldStylistId = oldStylistId,
                        NewStylistId = request.NewStylistId,
                        OldStylistName = oldStylistName,
                        NewStylistName = newStylistName,
                        OldDate = oldDate,
                        NewDate = request.NewDate.Date,
                        OldStartTime = oldStartTime,
                        NewStartTime = request.NewStartTime,
                        OldEndTime = oldEndTime,
                        NewEndTime = newEndTime,
                        Reason = request.Reason?.Trim(),
                        ChangedAtUtc = DateTime.UtcNow
                    };

                    _db.AppointmentChangeLogs.Add(changeLog);
                }

                // Cập nhật thông tin lịch hẹn
                appointment.StylistId = request.NewStylistId;
                appointment.AppointmentDate = request.NewDate.Date;
                appointment.StartTime = request.NewStartTime;
                appointment.EndTime = newEndTime;
                appointment.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(request.Reason))
                {
                    appointment.Notes = string.IsNullOrWhiteSpace(appointment.Notes)
                        ? $"[Dời lịch]: {request.Reason.Trim()}"
                        : $"{appointment.Notes}\n[Dời lịch]: {request.Reason.Trim()}";
                }

                await _db.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                var updatedCard = new AppointmentCalendarCardDto
                {
                    AppointmentId = appointment.AppointmentId,
                    CustomerId = appointment.CustomerId,
                    CustomerName = appointment.Customer?.FullName ?? "Khách lẻ",
                    CustomerPhone = appointment.Customer?.Phone ?? string.Empty,
                    StylistId = appointment.StylistId,
                    StylistName = newStylistName,
                    AppointmentDate = appointment.AppointmentDate,
                    StartTime = appointment.StartTime,
                    EndTime = appointment.EndTime,
                    DurationMinutes = totalDuration,
                    Status = appointment.Status,
                    ServiceNamesSummary = string.Join(", ", appointment.AppointmentServices.Select(s => s.Service?.ServiceName ?? "").Where(s => !string.IsNullOrEmpty(s))),
                    TotalAmount = appointment.AppointmentServices.Sum(s => s.Price)
                };

                return new RescheduleResult
                {
                    Success = true,
                    Message = "Đổi thợ và dời lịch hẹn thành công.",
                    Validation = validation,
                    UpdatedAppointment = updatedCard
                };
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        });
    }

    public async Task<List<AppointmentChangeLogDto>> GetRescheduleHistoryAsync(int appointmentId, CancellationToken cancellationToken = default)
    {
        var logs = await _db.AppointmentChangeLogs.AsNoTracking()
            .Where(l => l.AppointmentId == appointmentId)
            .OrderByDescending(l => l.ChangedAtUtc)
            .ToListAsync(cancellationToken);

        return logs.Select(l =>
        {
            var localTime = ToVietnamTime(l.ChangedAtUtc);
            var oldInfo = $"{l.OldStylistName ?? $"Thợ #{l.OldStylistId}"} ({l.OldDate:dd/MM/yyyy} {l.OldStartTime:hh\\:mm}-{l.OldEndTime:hh\\:mm})";
            var newInfo = $"{l.NewStylistName ?? $"Thợ #{l.NewStylistId}"} ({l.NewDate:dd/MM/yyyy} {l.NewStartTime:hh\\:mm}-{l.NewEndTime:hh\\:mm})";
            var summary = $"Chuyển từ [{oldInfo}] sang [{newInfo}]";

            return new AppointmentChangeLogDto
            {
                Id = l.Id,
                AppointmentId = l.AppointmentId,
                ModifiedByUserId = l.ModifiedByUserId,
                ModifiedByUserName = l.ModifiedByUserName,
                OldStylistId = l.OldStylistId,
                NewStylistId = l.NewStylistId,
                OldStylistName = l.OldStylistName,
                NewStylistName = l.NewStylistName,
                OldDate = l.OldDate,
                NewDate = l.NewDate,
                OldStartTime = l.OldStartTime,
                NewStartTime = l.NewStartTime,
                OldEndTime = l.OldEndTime,
                NewEndTime = l.NewEndTime,
                Reason = l.Reason,
                ChangedAtUtc = l.ChangedAtUtc,
                ChangedAtLocal = localTime,
                FormattedChangedAt = localTime.ToString("dd/MM/yyyy HH:mm:ss"),
                FormattedChangeSummary = summary
            };
        }).ToList();
    }

    private static DateTime ToVietnamTime(DateTime utcDateTime)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), tz);
        }
        catch
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), tz);
            }
            catch
            {
                return utcDateTime.AddHours(7);
            }
        }
    }
}

