using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;

namespace SalonManagement.Data;

public static class StylistAssignmentDemoSeed
{
    public static async Task SeedAsync(ApplicationDbContext db, DateTime firstDay)
    {
        // Day 3: A has 2, B has 1, C has 0. Day 4: A/B tie for cut + wash.
        var day = firstDay.Date.AddDays(2);
        var source = await db.Appointments.FirstOrDefaultAsync(a => a.Notes == "Demo S2-06" &&
            a.Stylist.FullName == "Demo Thợ A" && a.Customer.Notes == "Synthetic demo only" && a.AppointmentDate == day);
        if (source == null || await db.Appointments.AnyAsync(a => a.StylistId == source.StylistId &&
            a.AppointmentDate == day && a.Notes == "Demo S2-06 assignment")) return;
        db.Appointments.Add(new() { StylistId = source.StylistId, CustomerId = source.CustomerId,
            AppointmentDate = day, StartTime = TimeSpan.FromHours(12), EndTime = TimeSpan.FromHours(12.5),
            Status = "Pending", Notes = "Demo S2-06 assignment" });
        await db.SaveChangesAsync();
    }
}
