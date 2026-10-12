using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;

namespace SalonManagement.Data;

public static class DevelopmentBookingScheduleSeeder
{
    public static async Task SeedAsync(ApplicationDbContext dbContext, DateOnly today)
    {
        var stylists = await dbContext.Stylists
            .Where(stylist => stylist.IsActive)
            .Select(stylist => stylist.StylistId)
            .ToListAsync();

        for (var dayOffset = 1; dayOffset <= 14; dayOffset++)
        {
            var workDate = today.AddDays(dayOffset).ToDateTime(TimeOnly.MinValue);
            foreach (var stylistId in stylists)
            {
                // Preserve an existing shift or explicit day-off record.
                var hasSchedule = await dbContext.WorkSchedules.AnyAsync(schedule =>
                    schedule.StylistId == stylistId && schedule.WorkDate == workDate);
                if (hasSchedule)
                    continue;

                dbContext.WorkSchedules.Add(new WorkSchedule
                {
                    StylistId = stylistId,
                    WorkDate = workDate,
                    StartTime = new TimeSpan(8, 0, 0),
                    EndTime = new TimeSpan(17, 0, 0),
                    Status = "Working",
                    Notes = "Ca mẫu môi trường phát triển"
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
