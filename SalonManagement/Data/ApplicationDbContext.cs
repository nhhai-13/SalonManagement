using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;
namespace SalonManagement.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

public ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IHttpContextAccessor httpContextAccessor)
    : base(options)
{
    _httpContextAccessor = httpContextAccessor;
}

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Stylist> Stylists => Set<Stylist>();

        public DbSet<StylistService> StylistServices => Set<StylistService>();

        public DbSet<Service> Services => Set<Service>();

        public DbSet<ServiceGroup> ServiceGroups => Set<ServiceGroup>();

        public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
        public DbSet<StylistBreak> StylistBreaks => Set<StylistBreak>();
        public DbSet<StylistDayOff> StylistDaysOff => Set<StylistDayOff>();

        public DbSet<Appointment> Appointments => Set<Appointment>();

        public DbSet<AppointmentChangeLog> AppointmentChangeLogs => Set<AppointmentChangeLog>();
        public DbSet<AppointmentChangeEmail> AppointmentChangeEmails => Set<AppointmentChangeEmail>();

        public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();

        public DbSet<Invoice> Invoices => Set<Invoice>();

        public DbSet<Payment> Payments => Set<Payment>();

        public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();

        public DbSet<ShopHoliday> ShopHolidays => Set<ShopHoliday>();
        
        public DbSet<StylistTimeOff> StylistTimeOffs => Set<StylistTimeOff>();

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
public override int SaveChanges()
{
    return SaveChangesAsync().GetAwaiter().GetResult();
}

public override int SaveChanges(bool acceptAllChangesOnSuccess)
{
    if (!acceptAllChangesOnSuccess) throw new NotSupportedException("Audited saves require accepting changes.");
    return SaveChanges();
}

public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
{
    if (!acceptAllChangesOnSuccess) throw new NotSupportedException("Audited saves require accepting changes.");
    return SaveChangesAsync(cancellationToken);
}

public override async Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default)
{
    ChangeTracker.DetectChanges();
    if (ChangeTracker.Entries<AuditLog>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        throw new InvalidOperationException("Nhật ký hệ thống chỉ đọc, không được sửa hoặc xoá.");
    var auditEntries = CreateAuditEntries();

    var result = await base.SaveChangesAsync(true, cancellationToken);

    if (auditEntries.Count > 0)
    {
        foreach (var entry in auditEntries)
        {
            entry.EntityId = GetEntityId(entry.Entity);

            AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = _httpContextAccessor.HttpContext?.User?
                    .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value,
                UserName = _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
                    ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name,
                Action = entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId,
                Changes = entry.Changes
            });
        }

        await base.SaveChangesAsync(true, cancellationToken);
    }

    return result;
}

private List<AuditEntry> CreateAuditEntries()
{
    ChangeTracker.DetectChanges();

    var entries = new List<AuditEntry>();

    foreach (var entry in ChangeTracker.Entries())
    {
        if (entry.Entity is AuditLog ||
            entry.State == EntityState.Detached ||
            entry.State == EntityState.Unchanged)
        {
            continue;
        }

        var entityType = entry.Entity.GetType().Name;

        if (entityType != nameof(ApplicationUser) &&
            entry.Entity is not Microsoft.AspNetCore.Identity.IdentityUserRole<string> &&
            entityType != nameof(Service) &&
            entityType != nameof(WorkSchedule) &&
            entityType != nameof(Appointment) &&
            entityType != nameof(Invoice))
        {
            continue;
        }

        var changes = new Dictionary<string, object?>();

        if (entry.State == EntityState.Added)
        {
            foreach (var property in entry.Properties)
            {
                if (IsSensitiveAuditProperty(property.Metadata.Name)) continue;
                changes[property.Metadata.Name] = property.CurrentValue;
            }
        }
        else if (entry.State == EntityState.Modified)
        {
            foreach (var property in entry.Properties)
            {
                if (IsSensitiveAuditProperty(property.Metadata.Name)) continue;
                if (property.IsModified)
                {
                    changes[property.Metadata.Name] = new
                    {
                        OldValue = property.OriginalValue,
                        NewValue = property.CurrentValue
                    };
                }
            }
        }
        else if (entry.State == EntityState.Deleted)
        {
            foreach (var property in entry.Properties)
            {
                if (IsSensitiveAuditProperty(property.Metadata.Name)) continue;
                changes[property.Metadata.Name] = property.OriginalValue;
            }
        }

        entries.Add(new AuditEntry
        {
            Entity = entry.Entity,
            Action = entry.State switch
            {
                EntityState.Added => "CREATE",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted => "DELETE",
                _ => string.Empty
            },
            EntityType = entityType,
            Changes = JsonSerializer.Serialize(changes)
        });
    }

    return entries;
}

private static bool IsSensitiveAuditProperty(string name) => name is
    "PasswordHash" or "SecurityStamp" or "ConcurrencyStamp" or "EmailVerificationCodeHash";

private static string? GetEntityId(object entity)
{
    return entity switch
    {
        ApplicationUser user => user.Id,
        Microsoft.AspNetCore.Identity.IdentityUserRole<string> role => role.UserId,
        Service service => service.ServiceId.ToString(),
        WorkSchedule schedule => schedule.WorkScheduleId.ToString(),
        Appointment appointment => appointment.AppointmentId.ToString(),
        Invoice invoice => invoice.InvoiceId.ToString(),
        _ => null
    };
}

private class AuditEntry
{
    public object Entity { get; set; } = null!;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Changes { get; set; }
}
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(token => token.RefreshTokenId);
                entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
                entity.Property(token => token.UserId).IsRequired();
                entity.HasOne(token => token.User)
                    .WithMany(user => user.RefreshTokens)
                    .HasForeignKey(token => token.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(token => token.UserId);
                entity.HasIndex(token => token.TokenHash).IsUnique();
            });

            builder.Entity<BusinessHour>(entity =>
            {
                entity.HasKey(hours => hours.BusinessHourId);
                entity.Property(hours => hours.TimeZoneId).HasMaxLength(64);
                entity.HasIndex(hours => hours.DayOfWeek).IsUnique();
            });
            builder.Entity<ShopHoliday>(entity =>
            {
                entity.HasKey(holiday => holiday.ShopHolidayId);
                entity.Property(holiday => holiday.Reason).HasMaxLength(250);
                entity.HasIndex(holiday => holiday.HolidayDate).IsUnique();
            });
            
            builder.Entity<StylistTimeOff>(entity =>
           {
                entity.HasKey(timeOff => timeOff.StylistTimeOffId);
                entity.Property(timeOff => timeOff.Reason)
                    .HasMaxLength(250);
                entity.HasOne(timeOff => timeOff.Stylist)
                    .WithMany()
                    .HasForeignKey(timeOff => timeOff.StylistId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(timeOff => new
           {
                timeOff.StylistId,
                timeOff.OffDate
          });
        });

            builder.Entity<Customer>(entity =>
            {
                entity.HasKey(customer => customer.CustomerId);
                entity.Property(customer => customer.FullName).HasMaxLength(150);
                entity.Property(customer => customer.Phone).HasMaxLength(20);
                entity.Property(customer => customer.Email).HasMaxLength(256);
                entity.Property(customer => customer.Gender).HasMaxLength(20);
                entity.HasIndex(customer => customer.Phone);
            });

            builder.Entity<Stylist>(entity =>
            {
                entity.HasKey(stylist => stylist.StylistId);
                entity.Property(stylist => stylist.FullName).HasMaxLength(150);
                entity.Property(stylist => stylist.Phone).HasMaxLength(20);
                entity.Property(stylist => stylist.Email).HasMaxLength(256);
                entity.Property(stylist => stylist.Specialty).HasMaxLength(200);
                entity.Property(stylist => stylist.Description).HasMaxLength(500);
                entity.Property(stylist => stylist.ProfileImagePath).HasMaxLength(300);
            });

            builder.Entity<ServiceGroup>(entity =>
            {
                entity.HasKey(g => g.ServiceGroupId);
                entity.Property(g => g.GroupName).HasMaxLength(100).IsRequired();
            });

            builder.Entity<Service>(entity =>
            {
                entity.HasKey(service => service.ServiceId);
                entity.Property(service => service.ServiceName).HasMaxLength(150);
                entity.Property(service => service.Price).HasPrecision(18, 2);

                entity.HasOne(s => s.ServiceGroup)
                    .WithMany(g => g.Services)
                    .HasForeignKey(s => s.ServiceGroupId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<StylistService>(entity =>
            {
                entity.HasKey(item => new { item.StylistId, item.ServiceId });

                entity.HasOne(item => item.Stylist)
                    .WithMany(stylist => stylist.Services)
                    .HasForeignKey(item => item.StylistId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.Service)
                    .WithMany(service => service.Stylists)
                    .HasForeignKey(item => item.ServiceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WorkSchedule>(entity =>
            {
                entity.HasKey(schedule => schedule.WorkScheduleId);
                entity.Property(schedule => schedule.Status).HasMaxLength(30);

                entity.HasOne(schedule => schedule.Stylist)
                    .WithMany(stylist => stylist.WorkSchedules)
                    .HasForeignKey(schedule => schedule.StylistId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(schedule => new { schedule.StylistId, schedule.WorkDate });
            });

            builder.Entity<StylistBreak>(entity =>
            {
                entity.HasKey(item => item.StylistBreakId);
                entity.HasIndex(item => new { item.StylistId, item.BreakDate });
                entity.HasOne(item => item.Stylist).WithMany(stylist => stylist.Breaks).HasForeignKey(item => item.StylistId).OnDelete(DeleteBehavior.Cascade);
            });
            builder.Entity<StylistDayOff>(entity =>
            {
                entity.HasKey(item => item.StylistDayOffId);
                entity.HasIndex(item => new { item.StylistId, item.OffDate }).IsUnique();
                entity.HasOne(item => item.Stylist).WithMany(stylist => stylist.DaysOff).HasForeignKey(item => item.StylistId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Appointment>(entity =>
            {
                entity.HasKey(appointment => appointment.AppointmentId);
                entity.Property(appointment => appointment.Status).HasMaxLength(30);

                entity.HasOne(appointment => appointment.Customer)
                    .WithMany(customer => customer.Appointments)
                    .HasForeignKey(appointment => appointment.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(appointment => appointment.Stylist)
                    .WithMany(stylist => stylist.Appointments)
                    .HasForeignKey(appointment => appointment.StylistId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(appointment =>
                    new
                    {
                        appointment.StylistId,
                        appointment.AppointmentDate,
                        appointment.StartTime
                    });
            });

            builder.Entity<AppointmentChangeLog>(entity =>
            {
                entity.HasKey(log => log.AppointmentChangeLogId);
                entity.Property(log => log.ActorId).HasMaxLength(450).IsRequired();
                entity.Property(log => log.ActorName).HasMaxLength(256).IsRequired();
                entity.HasIndex(log => new { log.AppointmentId, log.ChangedAt });
            });

            builder.Entity<AppointmentChangeEmail>(entity =>
            {
                entity.HasKey(email => email.AppointmentChangeEmailId);
                entity.Property(email => email.RecipientEmail).HasMaxLength(256).IsRequired();
                entity.Property(email => email.StylistName).HasMaxLength(150).IsRequired();
                entity.Property(email => email.Status).HasMaxLength(20).IsRequired();
                entity.Property(email => email.LastError).HasMaxLength(1000);
                entity.HasIndex(email => new { email.Status, email.NextAttemptAt });
            });

            builder.Entity<AppointmentService>(entity =>
            {
                entity.HasKey(item => item.AppointmentServiceId);
                entity.Property(item => item.Price).HasPrecision(18, 2);

                entity.HasOne(item => item.Appointment)
                    .WithMany(appointment => appointment.AppointmentServices)
                    .HasForeignKey(item => item.AppointmentId);

                entity.HasOne(item => item.Service)
                    .WithMany(service => service.AppointmentServices)
                    .HasForeignKey(item => item.ServiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(item =>
                    new
                    {
                        item.AppointmentId,
                        item.ServiceId
                    })
                    .IsUnique();
            });

            builder.Entity<Invoice>(entity =>
            {
                entity.HasKey(invoice => invoice.InvoiceId);
                entity.Property(invoice => invoice.Subtotal).HasPrecision(18, 2);
                entity.Property(invoice => invoice.DiscountAmount).HasPrecision(18, 2);
                entity.Property(invoice => invoice.TotalAmount).HasPrecision(18, 2);
                entity.Property(invoice => invoice.Status).HasMaxLength(30);

                entity.HasOne(invoice => invoice.Appointment)
                    .WithOne(appointment => appointment.Invoice)
                    .HasForeignKey<Invoice>(invoice => invoice.AppointmentId);
            });

            builder.Entity<Payment>(entity =>
            {
                entity.HasKey(payment => payment.PaymentId);
                entity.Property(payment => payment.Amount).HasPrecision(18, 2);
                entity.Property(payment => payment.PaymentMethod).HasMaxLength(50);
                entity.Property(payment => payment.PaymentStatus).HasMaxLength(30);
                entity.Property(payment => payment.TransactionCode).HasMaxLength(100);

                entity.HasOne(payment => payment.Invoice)
                    .WithMany(invoice => invoice.Payments)
                    .HasForeignKey(payment => payment.InvoiceId);
            });
        }
    }
}

