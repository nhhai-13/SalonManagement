using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Data;

public static class Sprint4DemoSeed
{
    public static async Task SeedAsync(ApplicationDbContext db, DateTime today)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Notes == "Sprint 4 synthetic demo customer");
        if (customer is null)
        {
            customer = new Customer { FullName = "Khách mẫu Sprint 4", Phone = "0999000001", Email = "demo.customer@salon.local", Notes = "Sprint 4 synthetic demo customer" };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
        }
        var newCustomer = await db.Customers.FirstOrDefaultAsync(item => item.Notes == "Sprint 4 new customer sample");
        if (newCustomer is null)
        {
            newCustomer = new Customer { FullName = "Khách mới Sprint 4", Phone = "0999000002", Notes = "Sprint 4 new customer sample" };
            db.Customers.Add(newCustomer);
            await db.SaveChangesAsync();
        }
        var service = await db.Services.Where(item => item.IsActive).OrderBy(item => item.ServiceId).FirstOrDefaultAsync();
        var stylist = await db.Stylists.Where(item => item.IsActive).OrderBy(item => item.StylistId).FirstOrDefaultAsync();
        var secondStylist = await db.Stylists.Where(item => item.IsActive).OrderBy(item => item.StylistId).Skip(1).FirstOrDefaultAsync();
        if (service is null || stylist is null) return;

        for (var index = 1; index <= 12; index++)
        {
            var date = today.Date.AddDays(-index * 2);
            var reference = $"S4HIST{index:00}";
            var assignedStylist = index is >= 6 and <= 8 && secondStylist is not null ? secondStylist : stylist;
            var appointment = await db.Appointments.Include(item => item.AppointmentServices).Include(item => item.Invoice)
                .FirstOrDefaultAsync(item => item.BookingReference == reference);
            if (appointment is null)
            {
                var lineService = new AppointmentService
                {
                    ServiceId = service.ServiceId, StylistId = assignedStylist.StylistId, Price = service.Price,
                    DurationMinutes = service.DurationMinutes, IsCompleted = true, CompletedAt = date.AddHours(10),
                    TechnicalNotes = $"Ghi chú kỹ thuật mẫu lượt {index}", TechnicalNotesUpdatedBy = stylist.FullName,
                    TechnicalNotesUpdatedAt = DateTime.UtcNow.AddDays(-index * 2)
                };
                appointment = new Appointment
                {
                    CustomerId = customer.CustomerId, StylistId = assignedStylist.StylistId, AppointmentDate = date,
                    StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(10).Add(TimeSpan.FromMinutes(service.DurationMinutes)),
                    Status = "Closed", BookingReference = reference, Source = "WalkIn", Notes = "Sprint 4 synthetic history",
                    ClosedAt = date.AddHours(11), ClosedByName = "Demo lễ tân", AppointmentServices = [lineService]
                };
                var invoice = new Invoice { Appointment = appointment, Subtotal = service.Price, TotalAmount = service.Price,
                    Status = "Paid", CreatedAt = date.AddHours(11), ClosedAt = date.AddHours(11), ClosedByName = "Demo lễ tân",
                    Lines = [new InvoiceLine { AppointmentService = lineService, ServiceName = service.ServiceName, UnitPrice = service.Price }],
                    Payments = [new Payment { Amount = service.Price, PaymentMethod = "Cash", PaymentStatus = "Paid", PaidAt = date.AddHours(11), CreatedAt = date.AddHours(11) }] };
                appointment.Invoice = invoice;
                lineService.CollectedAmount = service.Price;
                db.Appointments.Add(appointment);
            }
            else
            {
                appointment.AppointmentDate = date;
                appointment.StylistId = assignedStylist.StylistId;
                appointment.AppointmentServices.FirstOrDefault()!.TechnicalNotes = $"Ghi chú kỹ thuật mẫu lượt {index}";
                appointment.AppointmentServices.FirstOrDefault()!.StylistId = assignedStylist.StylistId;
                if (appointment.Invoice is not null) appointment.Invoice.ClosedAt = date.AddHours(11);
            }
        }

        await db.SaveChangesAsync();
        for (var index = 1; index <= 12; index++)
        {
            var reference = $"S4HIST{index:00}";
            var appointment = await db.Appointments.FirstAsync(item => item.BookingReference == reference);
            if (await db.StylistReviews.AnyAsync(review => review.AppointmentId == appointment.AppointmentId)) continue;
            var assignedStylistId = index is >= 6 and <= 8 && secondStylist is not null ? secondStylist.StylistId : stylist.StylistId;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"staging-sample-review-{reference}")));
            db.StylistReviews.Add(new StylistReview
            {
                AppointmentId = appointment.AppointmentId,
                CustomerId = appointment.CustomerId,
                StylistId = assignedStylistId,
                ReviewTokenHash = hash,
                ExpiresAt = today.Date.AddYears(1),
                RequestedAt = today.Date.AddDays(-index * 2),
                SubmittedAt = today.Date.AddDays(-index * 2),
                Rating = index % 2 == 0 ? 5 : 4,
                Comment = "Đánh giá mẫu Sprint 4"
            });
        }

        var statusSamples = new[] { "Pending", "Confirmed", "Arrived", "InProgress", "Cancelled", "NoShow" };
        for (var index = 0; index < statusSamples.Length; index++)
        {
            var status = statusSamples[index];
            var marker = $"Sprint 4 demo status: {status}";
            var appointment = await db.Appointments.FirstOrDefaultAsync(item => item.Notes == marker);
            if (appointment is null)
            {
                appointment = new Appointment
                {
                    CustomerId = customer.CustomerId, StylistId = stylist.StylistId,
                    BookingReference = $"S4DAY{index + 1:00}", Source = "WalkIn", Notes = marker
                };
                db.Appointments.Add(appointment);
            }
            appointment.AppointmentDate = today.Date;
            appointment.StartTime = TimeSpan.FromHours(9 + index);
            appointment.EndTime = appointment.StartTime.Add(TimeSpan.FromMinutes(30));
            appointment.Status = status;
        }

        var firstVisit = await db.Appointments.FirstOrDefaultAsync(item => item.Notes == "Sprint 4 new customer appointment");
        if (firstVisit is null)
        {
            firstVisit = new Appointment { CustomerId = newCustomer!.CustomerId, StylistId = stylist.StylistId,
                BookingReference = "S4NEW01", Source = "Online", Notes = "Sprint 4 new customer appointment" };
            db.Appointments.Add(firstVisit);
        }
        firstVisit.AppointmentDate = today.Date;
        firstVisit.StartTime = TimeSpan.FromHours(16);
        firstVisit.EndTime = firstVisit.StartTime.Add(TimeSpan.FromMinutes(service.DurationMinutes));
        firstVisit.Status = "Confirmed";
        if (!await db.AppointmentServices.AnyAsync(item => item.AppointmentId == firstVisit.AppointmentId))
        {
            firstVisit.AppointmentServices.Add(new AppointmentService
            {
                ServiceId = service.ServiceId, StylistId = stylist.StylistId,
                Price = service.Price, DurationMinutes = service.DurationMinutes
            });
        }

        await SeedCoupon(db, "S4-10PCT", "Percent", 10m, today.Date.AddDays(-10), today.Date.AddDays(10));
        await SeedCoupon(db, "S4-EXPIRED", "Fixed", 50_000m, today.Date.AddDays(-20), today.Date.AddDays(-1));
        await db.SaveChangesAsync();
    }

    private static async Task SeedCoupon(ApplicationDbContext db, string code, string type, decimal value, DateTime starts, DateTime ends)
    {
        var coupon = await db.DiscountCoupons.FirstOrDefaultAsync(item => item.Code == code);
        if (coupon is null) db.DiscountCoupons.Add(new DiscountCoupon { Code = code, DiscountType = type, Value = value, StartsOn = starts, EndsOn = ends });
        else { coupon.StartsOn = starts; coupon.EndsOn = ends; }
    }
}
