using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;

namespace SalonManagement.Data;

public static class StylistBookingDemoSeed
{
    // Explicit opt-in in Development only; never edits real stylists or bookings.
    public static async Task SeedAsync(ApplicationDbContext db, DateTime day)
    {
        var group = await db.ServiceGroups.SingleOrDefaultAsync(g => g.GroupName == "Demo S2-06");
        if (group != null) return;
        var cut = new Service { ServiceName = "Demo Cắt tóc", DurationMinutes = 30, Price = 100000 };
        var wash = new Service { ServiceName = "Demo Gội đầu", DurationMinutes = 30, Price = 50000 };
        var dye = new Service { ServiceName = "Demo Nhuộm tóc", DurationMinutes = 60, Price = 300000 };
        var a = new Stylist { FullName = "Demo Thợ A", Specialty = "Cắt, gội", Services = [new() { Service = cut }, new() { Service = wash }] };
        var b = new Stylist { FullName = "Demo Thợ B", Specialty = "Cắt, gội, nhuộm", Services = [new() { Service = cut }, new() { Service = wash }, new() { Service = dye }] };
        var c = new Stylist { FullName = "Demo Thợ C", Specialty = "Cắt", Services = [new() { Service = cut }] };
        db.Add(new ServiceGroup { GroupName = "Demo S2-06", Services = [cut, wash, dye] });
        db.AddRange(a, b, c);
        var customer = new Customer { FullName = "Khách mẫu S2-06", Phone = "0000000000", Notes = "Synthetic demo only" };
        for (var i = 0; i < 7; i++)
        {
            var date = day.Date.AddDays(i);
            if (!await db.BusinessHours.AnyAsync(h => h.DayOfWeek == date.DayOfWeek))
                db.BusinessHours.Add(new() { DayOfWeek = date.DayOfWeek, OpensAt = new(9, 0), ClosesAt = new(17, 0) });
            foreach (var stylist in new[] { a, b, c })
                db.WorkSchedules.Add(new() { Stylist = stylist, WorkDate = date, StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(17) });
            db.Appointments.Add(new() { Stylist = a, Customer = customer, AppointmentDate = date,
                StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10), Status = "Confirmed", Notes = "Demo S2-06" });
            db.Appointments.Add(new() { Stylist = b, Customer = customer, AppointmentDate = date,
                StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = "Confirmed", Notes = "Demo S2-06" });
        }
        await db.SaveChangesAsync();
    }
}
