using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services
{
    public class AvailabilityService : IAvailabilityService
    {
        private readonly ApplicationDbContext _context;
        private const int SlotStepMinutes = 15;
        private const int MinLeadTimeMinutes = 60;
        private const int MaxDaysToLookAhead = 30;

        public AvailabilityService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================================================
        // 1. API CHÍNH: Lấy slot khả dụng trong 1 ngày
        // =============================================================
        public async Task<List<AvailabilitySlot>> GetAvailableSlotsAsync(
            DateTime date,
            List<int> serviceIds,
            int? stylistId = null)
        {
            if (serviceIds == null || serviceIds.Count == 0)
                return new List<AvailabilitySlot>();

            date = date.Date;

            var totalDuration = await GetTotalDurationAsync(serviceIds);
            if (totalDuration <= 0)
                return new List<AvailabilitySlot>();

            var businessHour = await GetBusinessHourAsync(date);
            if (businessHour == null || businessHour.IsClosed)
                return new List<AvailabilitySlot>();

            var eligibleStylists = await GetEligibleStylistsAsync(
                date, serviceIds, stylistId);

            if (!eligibleStylists.Any())
                return new List<AvailabilitySlot>();

            var slotsByStylist = new Dictionary<int, List<AvailabilitySlot>>();

            foreach (var stylist in eligibleStylists)
            {
                var stylistSlots = await CalculateSlotsForStylistAsync(
                    stylist.StylistId, date, totalDuration, businessHour);

                if (stylistSlots.Any())
                    slotsByStylist[stylist.StylistId] = stylistSlots;
            }

            if (!slotsByStylist.Any())
                return new List<AvailabilitySlot>();

            return MergeSlots(slotsByStylist);
        }

        // =============================================================
        // 2. API PHỤ: Tìm N ngày gần nhất có slot (cho AC5)
        // =============================================================
        public async Task<List<DateTime>> FindNextAvailableDatesAsync(
            DateTime fromDate,
            int count,
            List<int> serviceIds,
            int? stylistId = null)
        {
            var result = new List<DateTime>();
            fromDate = fromDate.Date;

            for (int i = 1; i <= MaxDaysToLookAhead && result.Count < count; i++)
            {
                var candidateDate = fromDate.AddDays(i);
                var slots = await GetAvailableSlotsAsync(
                    candidateDate, serviceIds, stylistId);

                if (slots.Any())
                    result.Add(candidateDate);
            }

            return result;
        }

        // =============================================================
        // 3. HELPER: Tổng thời lượng dịch vụ
        // =============================================================
        private async Task<int> GetTotalDurationAsync(List<int> serviceIds)
        {
            var durations = await _context.Services
                .AsNoTracking()
                .Where(s => serviceIds.Contains(s.ServiceId) && s.IsActive)
                .Select(s => s.DurationMinutes)
                .ToListAsync();

            return durations.Sum();
        }

        // =============================================================
        // 4. HELPER: Giờ mở cửa tiệm
        // =============================================================
        private async Task<BusinessHour?> GetBusinessHourAsync(DateTime date)
        {
            var dayOfWeek = date.DayOfWeek;

            return await _context.BusinessHours
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.DayOfWeek == dayOfWeek);
        }

        // =============================================================
        // 5. HELPER: Thợ đủ kỹ năng + có ca
        // =============================================================
        private async Task<List<Stylist>> GetEligibleStylistsAsync(
            DateTime date,
            List<int> serviceIds,
            int? stylistId)
        {
            var query = _context.Stylists
                .AsNoTracking()
                .Where(s => s.IsActive);

            if (stylistId.HasValue)
            {
                query = query.Where(s => s.StylistId == stylistId.Value);
            }

            query = query.Where(s => s.WorkSchedules
                .Any(w => w.WorkDate.Date == date.Date
                       && w.Status == "Working"));

            query = query.Where(s =>
                serviceIds.All(sid => s.Services.Any(ss => ss.ServiceId == sid)));

            return await query.ToListAsync();
        }

        // =============================================================
        // 6. HELPER: Tính slot cho 1 thợ
        // =============================================================
        private async Task<List<AvailabilitySlot>> CalculateSlotsForStylistAsync(
            int stylistId,
            DateTime date,
            int totalDuration,
            BusinessHour businessHour)
        {
            var workSchedules = await _context.WorkSchedules
                .AsNoTracking()
                .Where(w => w.StylistId == stylistId
                         && w.WorkDate.Date == date.Date
                         && w.Status == "Working")
                .OrderBy(w => w.StartTime)
                .ToListAsync();

            if (!workSchedules.Any())
                return new List<AvailabilitySlot>();

            var busyAppointments = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.StylistId == stylistId
                         && a.AppointmentDate.Date == date.Date
                         && AppointmentStatus.BusyStatuses.Contains(a.Status))
                .Select(a => new { a.StartTime, a.EndTime })
                .ToListAsync();

            var result = new List<AvailabilitySlot>();

            var openTime = businessHour.OpensAt!.Value.ToTimeSpan();
            var closeTime = businessHour.ClosesAt!.Value.ToTimeSpan();

            var now = DateTime.Now;
            TimeSpan? minStartTime = null;
            if (date.Date == now.Date)
            {
                minStartTime = now.TimeOfDay + TimeSpan.FromMinutes(MinLeadTimeMinutes);
            }

            foreach (var work in workSchedules)
            {
                var caStart = work.StartTime > openTime ? work.StartTime : openTime;
                var caEnd = work.EndTime < closeTime ? work.EndTime : closeTime;

                if (caEnd <= caStart)
                    continue;

                for (var slotStart = caStart;
                     slotStart + TimeSpan.FromMinutes(totalDuration) <= caEnd;
                     slotStart = slotStart.Add(TimeSpan.FromMinutes(SlotStepMinutes)))
                {
                    var slotEnd = slotStart + TimeSpan.FromMinutes(totalDuration);

                    if (minStartTime.HasValue && slotStart < minStartTime.Value)
                        continue;

                    bool hasConflict = busyAppointments.Any(b =>
                        slotStart < b.EndTime && slotEnd > b.StartTime);

                    if (hasConflict)
                        continue;

                    result.Add(new AvailabilitySlot
                    {
                        StartTime = slotStart,
                        EndTime = slotEnd,
                        AvailableStylistIds = new List<int> { stylistId }
                    });
                }
            }

            return result;
        }

        // =============================================================
        // 7. HELPER: Gộp slot nhiều thợ
        // =============================================================
        private List<AvailabilitySlot> MergeSlots(
            Dictionary<int, List<AvailabilitySlot>> slotsByStylist)
        {
            var merged = slotsByStylist
                .SelectMany(kv => kv.Value)
                .GroupBy(s => s.StartTime)
                .Select(g => new AvailabilitySlot
                {
                    StartTime = g.Key,
                    EndTime = g.First().EndTime,
                    AvailableStylistIds = g.SelectMany(s => s.AvailableStylistIds)
                                            .Distinct()
                                            .ToList()
                })
                .OrderBy(s => s.StartTime)
                .ToList();

            return merged;
        }
    }
}