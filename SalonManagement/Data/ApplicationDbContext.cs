using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;

namespace SalonManagement.Data
{
    public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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
        public DbSet<StylistTimeOff> StylistTimeOffs => Set<StylistTimeOff>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<AppointmentChangeLog> AppointmentChangeLogs => Set<AppointmentChangeLog>();
        public DbSet<AppointmentChangeEmail> AppointmentChangeEmails => Set<AppointmentChangeEmail>();
        public DbSet<AppointmentEmailDelivery> AppointmentEmailDeliveries => Set<AppointmentEmailDelivery>();
        public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();
        public DbSet<ScheduleContactFollowUp> ScheduleContactFollowUps => Set<ScheduleContactFollowUp>();
        public DbSet<ScheduleChangeNotification> ScheduleChangeNotifications => Set<ScheduleChangeNotification>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<DiscountCoupon> DiscountCoupons => Set<DiscountCoupon>();
        public DbSet<RehearsalRun> RehearsalRuns => Set<RehearsalRun>();
        public DbSet<RehearsalIssue> RehearsalIssues => Set<RehearsalIssue>();
        public DbSet<StagingEmailMessage> StagingEmailMessages => Set<StagingEmailMessage>();
        public DbSet<StylistReview> StylistReviews => Set<StylistReview>();

        public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
        public DbSet<InvoiceAdjustment> InvoiceAdjustments => Set<InvoiceAdjustment>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
        public DbSet<ShopHoliday> ShopHolidays => Set<ShopHoliday>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<RescheduleChangeLog> RescheduleChangeLogs => Set<RescheduleChangeLog>();

        // Keep audited async implementation as the single authoritative path.
        public override int SaveChanges()
            => SaveChanges(true);

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            if (!acceptAllChangesOnSuccess)
                throw new NotSupportedException("Audited saves require accepting changes.");

            // Call the audited async path synchronously.
            return SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (!acceptAllChangesOnSuccess)
                throw new NotSupportedException("Audited saves require accepting changes.");

            return SaveChangesAsync(cancellationToken);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ChangeTracker.DetectChanges();

            // Maintain existing behavior: assign missing booking refs if implemented in another partial.
            await AssignMissingBookingReferencesAsync(cancellationToken);

            // Prevent modification of audit logs.
            if (ChangeTracker.Entries<AuditLog>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
                throw new InvalidOperationException("Nhật ký hệ thống chỉ đọc, không được sửa hoặc xoá.");

            var auditEntries = CreateAuditEntries();

            // Save the current changes first (acceptAllChangesOnSuccess = true).
            var result = await base.SaveChangesAsync(true, cancellationToken).ConfigureAwait(false);

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

                // Persist audit logs.
                await base.SaveChangesAsync(true, cancellationToken).ConfigureAwait(false);
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
                // Skip auditing for identity and some heavy domain entities that we don't want in audit logs.
                if (entityType == nameof(ApplicationUser)
                    || entry.Entity is Microsoft.AspNetCore.Identity.IdentityUserRole<string>
                    || entityType == nameof(Service)
                    || entityType == nameof(WorkSchedule)
                    || entityType == nameof(Appointment)
                    || entityType == nameof(Invoice))
              

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
            ConfigureAttendanceModels(builder);

            // Keep the existing Identity key column limits from the initial schema.
            builder.Entity<IdentityUserLogin<string>>(entity =>
            {
                entity.Property(item => item.LoginProvider).HasMaxLength(128);
                entity.Property(item => item.ProviderKey).HasMaxLength(128);
            });
            builder.Entity<IdentityUserToken<string>>(entity =>
            {
                entity.Property(item => item.LoginProvider).HasMaxLength(128);
                entity.Property(item => item.Name).HasMaxLength(128);
            });

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
                entity.HasIndex(customer => customer.FullName);
            });

            builder.Entity<Appointment>().Property(appointment => appointment.Version).IsConcurrencyToken();

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

            builder.Entity<AppointmentEmailDelivery>(entity =>
            {
                entity.HasKey(delivery => delivery.AppointmentEmailDeliveryId);
                entity.Property(delivery => delivery.Kind).HasMaxLength(20).IsRequired();
                entity.Property(delivery => delivery.RecipientEmail).HasMaxLength(256);
                entity.Property(delivery => delivery.Status).HasMaxLength(20).IsRequired();
                entity.Property(delivery => delivery.LastError).HasMaxLength(1000);
                entity.HasIndex(delivery => new { delivery.AppointmentId, delivery.Kind }).IsUnique();
                entity.HasIndex(delivery => new { delivery.Status, delivery.NextAttemptAt });
                entity.HasOne(delivery => delivery.Appointment).WithMany(appointment => appointment.EmailDeliveries)
                    .HasForeignKey(delivery => delivery.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<AppointmentService>(entity =>
            {
                entity.HasKey(item => item.AppointmentServiceId);
                entity.Property(item => item.Price).HasPrecision(18, 2);
                entity.Property(item => item.CollectedAmount).HasPrecision(18, 2);
                entity.Property(item => item.TechnicalNotes).HasMaxLength(500);
                entity.Property(item => item.TechnicalNotesUpdatedBy).HasMaxLength(256);

                entity.HasOne(item => item.Appointment)
                    .WithMany(appointment => appointment.AppointmentServices)
                    .HasForeignKey(item => item.AppointmentId);

                entity.HasOne(item => item.Service)
                    .WithMany(service => service.AppointmentServices)
                    .HasForeignKey(item => item.ServiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(item => item.Stylist)
                    .WithMany(stylist => stylist.AppointmentServices)
                    .HasForeignKey(item => item.StylistId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(item => new { item.AppointmentId, item.ServiceId });
            });

            builder.Entity<ScheduleContactFollowUp>(entity =>
            {
                entity.HasKey(item => item.ScheduleContactFollowUpId);
                entity.Property(item => item.Category).HasMaxLength(30).IsRequired();
                entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
                entity.Property(item => item.ContactStatus).HasMaxLength(30).IsRequired();
                entity.Property(item => item.ContactNote).HasMaxLength(1000);
                entity.HasOne(item => item.Appointment)
                    .WithOne(appointment => appointment.ScheduleContactFollowUp)
                    .HasForeignKey<ScheduleContactFollowUp>(item => item.AppointmentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(item => item.AppointmentId).IsUnique();
                entity.HasIndex(item => new { item.OriginalAppointmentDate, item.ContactStatus, item.Category });
            });

            builder.Entity<ScheduleChangeNotification>(entity =>
            {
                entity.HasKey(item => item.ScheduleChangeNotificationId);
                entity.Property(item => item.RecipientEmail).HasMaxLength(256).IsRequired();
                entity.Property(item => item.CustomerName).HasMaxLength(150).IsRequired();
                entity.Property(item => item.ServiceNames).HasMaxLength(1000).IsRequired();
                entity.Property(item => item.OldStylistName).HasMaxLength(150).IsRequired();
                entity.Property(item => item.NewStylistName).HasMaxLength(150).IsRequired();
                entity.Property(item => item.Status).HasMaxLength(30).IsRequired();
                entity.Property(item => item.LastError).HasMaxLength(1000);
                entity.HasOne(item => item.Appointment)
                    .WithMany(appointment => appointment.ScheduleChangeNotifications)
                    .HasForeignKey(item => item.AppointmentId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(item => new { item.AppointmentId, item.Version }).IsUnique();
                entity.HasIndex(item => new { item.Status, item.NextAttemptAt });
            });

            builder.Entity<Invoice>(entity =>
            {
                entity.HasKey(invoice => invoice.InvoiceId);
                entity.Property(invoice => invoice.Subtotal).HasPrecision(18, 2);
                entity.Property(invoice => invoice.DiscountAmount).HasPrecision(18, 2);
                entity.Property(invoice => invoice.DiscountValueSnapshot).HasPrecision(18, 2);
                entity.Property(invoice => invoice.DiscountCodeSnapshot).HasMaxLength(32);
                entity.Property(invoice => invoice.DiscountTypeSnapshot).HasMaxLength(16);
                entity.HasOne(invoice => invoice.DiscountCoupon)
                    .WithMany(coupon => coupon.Invoices)
                    .HasForeignKey(invoice => invoice.DiscountCouponId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.Property(invoice => invoice.TotalAmount).HasPrecision(18, 2);
                entity.Property(invoice => invoice.Status).HasMaxLength(30);

                entity.HasOne(invoice => invoice.Appointment)
                    .WithOne(appointment => appointment.Invoice)
                    .HasForeignKey<Invoice>(invoice => invoice.AppointmentId);

                entity.HasIndex(invoice => invoice.AppointmentId).IsUnique();
            });

            builder.Entity<DiscountCoupon>(entity =>
            {
                entity.HasKey(coupon => coupon.DiscountCouponId);
                entity.HasIndex(coupon => coupon.Code).IsUnique();
                entity.Property(coupon => coupon.Code).HasMaxLength(32).IsRequired();
                entity.Property(coupon => coupon.DiscountType).HasMaxLength(16).IsRequired();
                entity.Property(coupon => coupon.Value).HasPrecision(18, 2);
            });

            builder.Entity<RehearsalRun>(entity =>
            {
                entity.HasKey(run => run.RehearsalRunId);
                entity.Property(run => run.PerformedBy).HasMaxLength(256).IsRequired();
                entity.Property(run => run.Notes).HasMaxLength(1000);
            });

            builder.Entity<RehearsalIssue>(entity =>
            {
                entity.HasKey(issue => issue.RehearsalIssueId);
                entity.Property(issue => issue.Step).HasMaxLength(200).IsRequired();
                entity.Property(issue => issue.Account).HasMaxLength(128).IsRequired();
                entity.Property(issue => issue.Observation).HasMaxLength(2000).IsRequired();
                entity.Property(issue => issue.Severity).HasMaxLength(16).IsRequired();
                entity.Property(issue => issue.Status).HasMaxLength(24).IsRequired();
                entity.Property(issue => issue.Assignee).HasMaxLength(256);
                entity.Property(issue => issue.LastChangedBy).HasMaxLength(256);
                entity.HasOne(issue => issue.Run).WithMany(run => run.Issues)
                    .HasForeignKey(issue => issue.RehearsalRunId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<StagingEmailMessage>(entity =>
            {
                entity.HasKey(message => message.StagingEmailMessageId);
                entity.Property(message => message.Template).HasMaxLength(40).IsRequired();
                entity.Property(message => message.Recipient).HasMaxLength(256).IsRequired();
                entity.Property(message => message.Summary).HasMaxLength(500).IsRequired();
                entity.HasIndex(message => message.CreatedAt);
            });

            builder.Entity<StylistReview>(entity =>
            {
                entity.HasKey(review => review.StylistReviewId);
                entity.Property(review => review.ReviewTokenHash).HasMaxLength(64).IsRequired();
                entity.Property(review => review.Comment).HasMaxLength(1000);
                entity.HasIndex(review => review.AppointmentId).IsUnique();
                entity.HasIndex(review => review.ReviewTokenHash).IsUnique();
                entity.HasOne(review => review.Appointment).WithOne().HasForeignKey<StylistReview>(review => review.AppointmentId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(review => review.Customer).WithMany().HasForeignKey(review => review.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(review => review.Stylist).WithMany().HasForeignKey(review => review.StylistId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<InvoiceLine>(entity =>
            {
                entity.HasKey(line => line.InvoiceLineId);
                entity.Property(line => line.ServiceName).HasMaxLength(150).IsRequired();
                entity.Property(line => line.UnitPrice).HasPrecision(18, 2);
                entity.HasOne(line => line.Invoice)
                    .WithMany(invoice => invoice.Lines)
                    .HasForeignKey(line => line.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(line => line.AppointmentService)
                    .WithOne(item => item.InvoiceLine)
                    .HasForeignKey<InvoiceLine>(line => line.AppointmentServiceId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(line => line.AppointmentServiceId).IsUnique();
            });

            builder.Entity<InvoiceAdjustment>(entity =>
            {
                entity.HasKey(adjustment => adjustment.InvoiceAdjustmentId);
                entity.Property(adjustment => adjustment.Description).HasMaxLength(150).IsRequired();
                entity.Property(adjustment => adjustment.AmountDelta).HasPrecision(18, 2);
                entity.Property(adjustment => adjustment.Reason).HasMaxLength(500).IsRequired();
                entity.Property(adjustment => adjustment.CreatedByUserId).HasMaxLength(450).IsRequired();
                entity.Property(adjustment => adjustment.CreatedByName).HasMaxLength(256).IsRequired();
                entity.HasOne(adjustment => adjustment.Invoice)
                    .WithMany(invoice => invoice.Adjustments)
                    .HasForeignKey(adjustment => adjustment.InvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(adjustment => adjustment.InvoiceLine)
                    .WithMany()
                    .HasForeignKey(adjustment => adjustment.InvoiceLineId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(adjustment => adjustment.Stylist)
                    .WithMany(stylist => stylist.InvoiceAdjustments)
                    .HasForeignKey(adjustment => adjustment.StylistId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Payment>(entity =>
            {
                entity.HasKey(payment => payment.PaymentId);
                entity.Property(payment => payment.Amount).HasPrecision(18, 2);
                entity.Property(payment => payment.PaymentMethod).HasMaxLength(50);
                entity.Property(payment => payment.PaymentStatus).HasMaxLength(30);
                entity.Property(payment => payment.TransactionCode).HasMaxLength(500);

                entity.HasOne(payment => payment.Invoice)
                    .WithMany(invoice => invoice.Payments)
                    .HasForeignKey(payment => payment.InvoiceId);
            });

            // Cấu hình lịch nghỉ riêng của thợ
            builder.Entity<StylistTimeOff>(entity =>
            {
                entity.HasKey(timeOff => timeOff.StylistTimeOffId);

                entity.HasOne(timeOff => timeOff.Stylist)
                    .WithMany()
                    .HasForeignKey(timeOff => timeOff.StylistId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<RescheduleChangeLog>(entity =>
            {
                entity.HasKey(log => log.Id);
                entity.Property(log => log.ModifiedByUserId).HasMaxLength(450);
                entity.Property(log => log.ModifiedByUserName).HasMaxLength(256);
                entity.Property(log => log.OldStylistName).HasMaxLength(150);
                entity.Property(log => log.NewStylistName).HasMaxLength(150);
                entity.Property(log => log.Reason).HasMaxLength(500);

                entity.HasOne(log => log.Appointment)
                    .WithMany()
                    .HasForeignKey(log => log.AppointmentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(log => log.AppointmentId);
                entity.HasIndex(log => log.ChangedAtUtc);
            });
        }
    }
}

