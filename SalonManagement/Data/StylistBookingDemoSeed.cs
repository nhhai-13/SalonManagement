using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;

namespace SalonManagement.Data;

public static class StylistBookingDemoSeed
{
    // Explicit opt-in in Development only. It reuses the seeded salon employees,
    // so test data never appears as fictional staff in the customer booking flow.
    public static async Task SeedAsync(ApplicationDbContext db, DateTime day)
    {
        var group = await db.ServiceGroups.SingleOrDefaultAsync(g => g.GroupName == "Demo S2-06");
        if (group != null)
        {
            // Use the staff already seeded in the salon database. The old demo
            // accounts are retained only for historical sample bookings and are
            // hidden from all booking choices.
            var demoAccounts = await db.Stylists
                .Where(s => s.FullName == "Demo Thợ A" || s.FullName == "Demo Thợ B" || s.FullName == "Demo Thợ C")
                .ToListAsync();

            var bookingStylistIds = await db.Stylists
                .Where(s => s.IsActive && s.FullName != "Demo Thợ A" && s.FullName != "Demo Thợ B" && s.FullName != "Demo Thợ C")
                .OrderBy(s => s.StylistId)
                .Select(s => s.StylistId)
                .Take(3)
                .ToListAsync();
            if (bookingStylistIds.Count == 0)
            {
                // Isolated automated tests contain only synthetic staff. Keep
                // them available there; production/development data always
                // takes the real-staff branch below.
                bookingStylistIds = demoAccounts.Select(account => account.StylistId).ToList();
            }
            else
            {
                foreach (var account in demoAccounts)
                    account.IsActive = false;
            }
            if (bookingStylistIds.Count == 0) return;

            var demoServiceIds = await db.Services
                .Where(s => s.ServiceGroupId == group.ServiceGroupId)
                .Select(s => s.ServiceId)
                .ToListAsync();
            var existingLinkRows = await db.StylistServices
                .Where(link => bookingStylistIds.Contains(link.StylistId) && demoServiceIds.Contains(link.ServiceId))
                .Select(link => new { link.StylistId, link.ServiceId })
                .ToListAsync();
            var existingLinks = existingLinkRows
                .Select(link => (link.StylistId, link.ServiceId))
                .ToHashSet();
            foreach (var stylistId in bookingStylistIds)
            foreach (var serviceId in demoServiceIds)
            {
                if (!existingLinks.Contains((stylistId, serviceId)))
                    db.StylistServices.Add(new StylistService { StylistId = stylistId, ServiceId = serviceId });
            }

            // Keep the opt-in demo usable after a restart as well. Older local
            // databases may contain the group but not all of its work shifts.
            var endDate = day.Date.AddDays(7);
            var existingShifts = await db.WorkSchedules
                .Where(s => bookingStylistIds.Contains(s.StylistId) && s.WorkDate >= day.Date && s.WorkDate < endDate)
                .ToListAsync();
            foreach (var shift in existingShifts)
                shift.Status = "Working";

            var existingShiftKeys = existingShifts
                .Select(s => (s.StylistId, s.WorkDate.Date))
                .ToHashSet();
            for (var i = 0; i < 7; i++)
            {
                var workDate = day.Date.AddDays(i);
                foreach (var stylistId in bookingStylistIds)
                {
                    if (!existingShiftKeys.Contains((stylistId, workDate)))
                    {
                        db.WorkSchedules.Add(new WorkSchedule
                        {
                            StylistId = stylistId,
                            WorkDate = workDate,
                            StartTime = TimeSpan.FromHours(9),
                            EndTime = TimeSpan.FromHours(17),
                            Status = "Working"
                        });
                    }
                }
            }
            await db.SaveChangesAsync();

            // Upgrade an existing opt-in demo without rewriting any appointments.
            var fullDay = day.Date.AddDays(1);
            var demo = await db.Appointments.FirstOrDefaultAsync(a => a.Notes == "Demo S2-06" &&
                a.Stylist.FullName == "Demo Thợ A" && a.AppointmentDate == fullDay &&
                a.Customer.Notes == "Synthetic demo only");
            if (demo != null && demo.EndTime < TimeSpan.FromHours(17) &&
                !await db.Appointments.AnyAsync(a => a.StylistId == demo.StylistId &&
                    a.AppointmentDate == fullDay && a.Notes == "Demo S2-06 any stylist"))
            {
                db.Appointments.Add(new() { StylistId = demo.StylistId, CustomerId = demo.CustomerId,
                    AppointmentDate = fullDay, StartTime = demo.EndTime, EndTime = TimeSpan.FromHours(17),
                    Status = "Confirmed", Notes = "Demo S2-06 any stylist" });
                await db.SaveChangesAsync();
            }
            return;
        }
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
                StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(i == 1 ? 17 : 10), Status = "Confirmed", BookingReference = $"D{date:MMdd}{i:00}A", Notes = "Demo S2-06" });
            db.Appointments.Add(new() { Stylist = b, Customer = customer, AppointmentDate = date,
                StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = "Confirmed", BookingReference = $"D{date:MMdd}{i:00}B", Notes = "Demo S2-06" });
        }
        await db.SaveChangesAsync();
    }
}
