using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;

namespace SalonManagement.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Stylist> Stylists => Set<Stylist>();

        public DbSet<Service> Services => Set<Service>();

        public DbSet<ServiceGroup> ServiceGroups => Set<ServiceGroup>();

        public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();

        public DbSet<Appointment> Appointments => Set<Appointment>();

        public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();

        public DbSet<Invoice> Invoices => Set<Invoice>();

        public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

        public DbSet<InvoiceAdjustment> InvoiceAdjustments => Set<InvoiceAdjustment>();

        public DbSet<Payment> Payments => Set<Payment>();

        public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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

                entity.HasIndex(invoice => invoice.AppointmentId).IsUnique();
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
        }
    }
}

