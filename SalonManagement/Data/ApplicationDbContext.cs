using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
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

        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Stylist> Stylists => Set<Stylist>();

        public DbSet<Service> Services => Set<Service>();

        public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();

        public DbSet<Appointment> Appointments => Set<Appointment>();

        public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();

        public DbSet<Invoice> Invoices => Set<Invoice>();

        public DbSet<Payment> Payments => Set<Payment>();

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(token => token.RefreshTokenId);
                entity.Property(token => token.TokenHash).HasMaxLength(64);
                entity.HasIndex(token => token.TokenHash).IsUnique();
                entity.HasOne(token => token.User)
                    .WithMany(user => user.RefreshTokens)
                    .HasForeignKey(token => token.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
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

            builder.Entity<Service>(entity =>
            {
                entity.HasKey(service => service.ServiceId);
                entity.Property(service => service.ServiceName).HasMaxLength(150);
                entity.Property(service => service.Price).HasPrecision(18, 2);
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
                    new { appointment.StylistId, appointment.AppointmentDate, appointment.StartTime });
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

                entity.HasIndex(item => new { item.AppointmentId, item.ServiceId }).IsUnique();
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
