using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;
namespace SalonManagement.Data;
public partial class ApplicationDbContext
{
    public DbSet<AppointmentAudit> AppointmentAudits => Set<AppointmentAudit>();
    public DbSet<StylistNotification> StylistNotifications => Set<StylistNotification>();
    private static void ConfigureAttendanceModels(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>().HasOne(u => u.Stylist).WithMany().HasForeignKey(u => u.StylistId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Appointment>().Property(a => a.Version).IsConcurrencyToken();
        builder.Entity<AppointmentAudit>().Property(a => a.ActorId).HasMaxLength(450);
        builder.Entity<AppointmentAudit>().HasIndex(a => new { a.AppointmentId, a.OccurredAt });
        builder.Entity<StylistNotification>().HasIndex(n => new { n.StylistId, n.CreatedAt });
    }
}
