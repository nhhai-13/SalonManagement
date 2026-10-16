using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Controllers.Owner;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;
using Xunit;

namespace SalonManagement.Tests;

public sealed class OwnerOperationsDashboardTests
{
    [Fact]
    public async Task Get_totals_paid_decimal_amounts_when_running_on_sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new HttpContextAccessor());
        await db.Database.EnsureCreatedAsync();

        var date = new DateTime(2026, 10, 15);
        var customer = new Customer { FullName = "Khách demo", NormalizedSearchName = "khachdemo", Phone = "0909000199" };
        var stylist = new Stylist { FullName = "Thợ demo", Phone = "0909000198", IsActive = true };
        db.AddRange(customer, stylist);
        await db.SaveChangesAsync();
        db.WorkSchedules.Add(new WorkSchedule { StylistId = stylist.StylistId, WorkDate = date, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(17), Status = "Working" });
        var appointment = new Appointment { CustomerId = customer.CustomerId, StylistId = stylist.StylistId, AppointmentDate = date, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(11), Status = "Completed", BookingReference = "OWNER-TEST" };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        var invoice = new Invoice { AppointmentId = appointment.AppointmentId, Subtotal = 150000m, DiscountAmount = 0, TotalAmount = 150000m, Status = "Paid" };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        db.Payments.Add(new Payment { InvoiceId = invoice.InvoiceId, Amount = 150000m, PaymentMethod = "Cash", PaymentStatus = "Paid", PaidAt = date.AddHours(10) });
        await db.SaveChangesAsync();

        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 15, 3, 0, 0, TimeSpan.Zero));
        var controller = new OwnerOperationsDashboardController(db, clock, new AppointmentEmailNotificationService(db, clock));

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var snapshot = Assert.IsType<OwnerOperationsSnapshot>(ok.Value);
        Assert.Equal(150000m, snapshot.RevenueToday);
        Assert.Equal(1, snapshot.Statuses.Completed);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
