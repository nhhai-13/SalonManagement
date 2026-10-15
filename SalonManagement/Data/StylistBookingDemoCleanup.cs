using Microsoft.EntityFrameworkCore;

namespace SalonManagement.Data;

/// <summary>Removes the retired S2-06 sample catalog from development data.
/// It is deliberately scoped to the exact demo group and its marked bookings.</summary>
public static class StylistBookingDemoCleanup
{
    public static async Task RemoveAsync(ApplicationDbContext db)
    {
        var group = await db.ServiceGroups
            .SingleOrDefaultAsync(item => item.GroupName == "Demo S2-06");
        if (group is null) return;

        var serviceIds = await db.Services
            .Where(item => item.ServiceGroupId == group.ServiceGroupId)
            .Select(item => item.ServiceId)
            .ToListAsync();

        var demoAppointments = await db.Appointments
            .Where(item => item.Notes != null && item.Notes.StartsWith("Demo S2-06"))
            .ToListAsync();
        if (demoAppointments.Count > 0) db.Appointments.RemoveRange(demoAppointments);

        if (serviceIds.Count > 0)
        {
            var appointmentServices = await db.AppointmentServices
                .Where(item => serviceIds.Contains(item.ServiceId))
                .ToListAsync();
            var stylistServices = await db.StylistServices
                .Where(item => serviceIds.Contains(item.ServiceId))
                .ToListAsync();
            if (appointmentServices.Count > 0) db.AppointmentServices.RemoveRange(appointmentServices);
            if (stylistServices.Count > 0) db.StylistServices.RemoveRange(stylistServices);

            var services = await db.Services.Where(item => serviceIds.Contains(item.ServiceId)).ToListAsync();
            db.Services.RemoveRange(services);
        }

        db.ServiceGroups.Remove(group);
        await db.SaveChangesAsync();
    }
}
