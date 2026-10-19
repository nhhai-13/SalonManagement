using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Data;

/// <summary>
/// Opt-in staging fixtures. The records use stable STG-* references and are
/// idempotent so restarting the demo does not duplicate appointments or money.
/// </summary>
public static class StagingDemoSeed
{
    private const string DemoPassword = "Demo123!";

    public static async Task SeedAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, DateTime now)
    {
        await EnsureUserAsync(users, "customer_demo@luminol.vn", "Khách hàng Demo", "0909000101", UserRoles.Customer);
        await EnsureUserAsync(users, "letan_demo@luminol.vn", "Lễ tân Demo", "0909000102", UserRoles.Receptionist);
        await EnsureUserAsync(users, "stylist_demo@luminol.vn", "Thợ Demo", "0909000103", UserRoles.Stylist);
        await EnsureUserAsync(users, "owner_demo@luminol.vn", "Chủ tiệm Demo", "0909000104", UserRoles.Owner);

        await StylistProfileSynchronizer.EnsureForActiveStylistAccountsAsync(db);

        var stylist = await db.Stylists.SingleAsync(item => item.Email == "stylist_demo@luminol.vn");
        stylist.FullName = "Thợ Demo";
        stylist.Phone = "0909000103";
        stylist.Specialty = "Cắt tóc, gội đầu và nhuộm";
        stylist.IsActive = true;

        var services = await db.Services.Where(item => item.IsActive)
            .OrderBy(item => item.ServiceId).Take(3).ToListAsync();
        if (services.Count < 3)
            throw new InvalidOperationException("Không thể nạp dữ liệu staging vì chưa có dịch vụ mẫu.");

        var serviceIds = services.Select(item => item.ServiceId).ToHashSet();
        var linkedIds = await db.StylistServices.Where(item => item.StylistId == stylist.StylistId)
            .Select(item => item.ServiceId).ToListAsync();
        foreach (var serviceId in serviceIds.Except(linkedIds))
            db.StylistServices.Add(new StylistService { StylistId = stylist.StylistId, ServiceId = serviceId });

        var today = now.Date;
        await EnsureShiftAsync(db, stylist.StylistId, today);
        await EnsureShiftAsync(db, stylist.StylistId, today.AddMonths(-1));
        await db.SaveChangesAsync();

        var customer = await EnsureCustomerAsync(db, "Khách hàng Demo", "0909000101", "customer_demo@luminol.vn");
        var walkIn = await EnsureCustomerAsync(db, "Nguyễn An Nhiên", "0909000105", null);
        var reportCustomer = await EnsureCustomerAsync(db, "Trần Minh Báo", "0909000106", "bao.cao.demo@luminol.vn");
        await db.SaveChangesAsync();

        // A full set of live statuses keeps the owner dashboard and reception
        // pages meaningful immediately after staging starts.
        await EnsureAppointmentAsync(db, "STG-PENDING", customer, stylist, services[0], today, TimeSpan.FromHours(9), "PendingConfirmation", now, paid: false);
        await EnsureAppointmentAsync(db, "STG-CONFIRMED", customer, stylist, services[0], today, TimeSpan.FromHours(10), "Confirmed", now, paid: false);
        await EnsureAppointmentAsync(db, "STG-ARRIVED", walkIn, stylist, services[1], today, TimeSpan.FromHours(11), "Arrived", now, paid: false);
        await EnsureAppointmentAsync(db, "STG-PAID", customer, stylist, services[2], today, TimeSpan.FromHours(13), "Completed", now, paid: true);
        await EnsureAppointmentAsync(db, "STG-NOSHOW", walkIn, stylist, services[0], today, TimeSpan.FromHours(15), "NoShow", now, paid: false);
        await EnsureAppointmentAsync(db, "STG-CANCELLED", reportCustomer, stylist, services[1], today, TimeSpan.FromHours(16), "Cancelled", now, paid: false);

        // These paid records intentionally sit in the preceding month. They
        // prevent the "previous month has no revenue" dry-run regression.
        var lastMonth = new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, Math.Min(15, DateTime.DaysInMonth(today.AddMonths(-1).Year, today.AddMonths(-1).Month)));
        await EnsureAppointmentAsync(db, "STG-PREV-01", reportCustomer, stylist, services[0], lastMonth, TimeSpan.FromHours(10), "Completed", lastMonth.AddHours(11), paid: true);
        await EnsureAppointmentAsync(db, "STG-PREV-02", customer, stylist, services[1], lastMonth.AddDays(2), TimeSpan.FromHours(14), "Completed", lastMonth.AddDays(2).AddHours(15), paid: true);
        await EnsureAppointmentAsync(db, "STG-PREV-03", walkIn, stylist, services[2], lastMonth.AddDays(5), TimeSpan.FromHours(16), "Completed", lastMonth.AddDays(5).AddHours(18), paid: true);

        if (!await db.PromotionCodes.AnyAsync(item => item.Code == "DEMO10"))
        {
            db.PromotionCodes.Add(new PromotionCode
            {
                Code = "DEMO10",
                Name = "Ưu đãi demo staging 10%",
                DiscountType = "Percentage",
                DiscountValue = 10,
                StartsAt = today.AddDays(-7),
                EndsAt = today.AddDays(30),
                IsActive = true
            });
        }

        var paidAppointment = await db.Appointments.SingleAsync(item => item.BookingReference == "STG-PAID");
        if (!await db.ServiceReviews.AnyAsync(item => item.AppointmentId == paidAppointment.AppointmentId))
        {
            db.ServiceReviews.Add(new ServiceReview
            {
                AppointmentId = paidAppointment.AppointmentId,
                TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("staging-review-stg-paid"))),
                CreatedAt = now,
                EmailSentAt = now,
                ExpiresAt = now.AddDays(7)
            });
        }

        if (!await db.StaffNotifications.AnyAsync(item => item.RecipientEmail == "stylist_demo@luminol.vn" && item.AppointmentId == paidAppointment.AppointmentId && item.Type == "CheckIn"))
        {
            db.StaffNotifications.Add(new StaffNotification
            {
                RecipientEmail = "stylist_demo@luminol.vn",
                AppointmentId = paidAppointment.AppointmentId,
                Type = "CheckIn",
                Message = "Khách Khách hàng Demo đã check-in. Bấm để mở lịch STG-PAID.",
                CreatedAt = now.ToUniversalTime()
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, string email, string name, string phone, string role)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = name,
                PhoneNumber = phone,
                IsActive = true
            };
            var result = await users.CreateAsync(user, DemoPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Không thể tạo tài khoản staging {email}: {string.Join(", ", result.Errors.Select(item => item.Description))}");
        }

        if (await users.FindByEmailAsync(email) is not null && user.PasswordHash is not null)
        {
            var resetToken = await users.GeneratePasswordResetTokenAsync(user);
            var reset = await users.ResetPasswordAsync(user, resetToken, DemoPassword);
            if (!reset.Succeeded)
                throw new InvalidOperationException($"Không thể đặt lại mật khẩu staging {email}: {string.Join(", ", reset.Errors.Select(item => item.Description))}");
        }

        user.EmailConfirmed = true;
        user.IsActive = true;
        user.FullName = name;
        user.PhoneNumber = phone;
        var update = await users.UpdateAsync(user);
        if (!update.Succeeded)
            throw new InvalidOperationException($"Không thể cập nhật tài khoản staging {email}: {string.Join(", ", update.Errors.Select(item => item.Description))}");
        if (!await users.IsInRoleAsync(user, role))
        {
            var roleResult = await users.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Không thể gán quyền {role} cho {email}: {string.Join(", ", roleResult.Errors.Select(item => item.Description))}");
        }
    }

    private static async Task<Customer> EnsureCustomerAsync(ApplicationDbContext db, string name, string phone, string? email)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.Phone == phone);
        if (customer is not null) return customer;
        customer = new Customer
        {
            FullName = name,
            NormalizedSearchName = NormalizeName(name),
            Phone = phone,
            Email = email,
            Notes = "STAGING-DEMO",
            IsActive = true
        };
        db.Customers.Add(customer);
        return customer;
    }

    private static async Task EnsureShiftAsync(ApplicationDbContext db, int stylistId, DateTime date)
    {
        var shift = await db.WorkSchedules.SingleOrDefaultAsync(item => item.StylistId == stylistId && item.WorkDate == date.Date);
        if (shift is not null)
        {
            shift.StartTime = TimeSpan.FromHours(8);
            shift.EndTime = TimeSpan.FromHours(20);
            shift.Status = "Working";
            return;
        }
        db.WorkSchedules.Add(new WorkSchedule
        {
            StylistId = stylistId,
            WorkDate = date.Date,
            StartTime = TimeSpan.FromHours(8),
            EndTime = TimeSpan.FromHours(20),
            Status = "Working",
            Notes = "STAGING-DEMO"
        });
    }

    private static async Task EnsureAppointmentAsync(ApplicationDbContext db, string reference, Customer customer, Stylist stylist, Service service, DateTime date, TimeSpan start, string status, DateTime completedOrSeededAt, bool paid)
    {
        if (await db.Appointments.AnyAsync(item => item.BookingReference == reference)) return;
        var appointment = new Appointment
        {
            CustomerId = customer.CustomerId,
            StylistId = stylist.StylistId,
            AppointmentDate = date.Date,
            StartTime = start,
            EndTime = start.Add(TimeSpan.FromMinutes(service.DurationMinutes)),
            Status = status,
            BookingReference = reference,
            BookingSource = "StagingDemo",
            Notes = "STAGING-DEMO",
            ArrivedAt = status is "Arrived" or "Completed" ? completedOrSeededAt : null,
            ActualStartedAt = status == "Completed" ? completedOrSeededAt.AddMinutes(-service.DurationMinutes) : null,
            ActualCompletedAt = status == "Completed" ? completedOrSeededAt : null,
            CancelledAt = status == "Cancelled" ? completedOrSeededAt : null,
            NoShowAt = status == "NoShow" ? completedOrSeededAt : null
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        db.AppointmentServices.Add(new AppointmentService
        {
            AppointmentId = appointment.AppointmentId,
            ServiceId = service.ServiceId,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes
        });
        await db.SaveChangesAsync();

        if (!paid) return;
        var discount = reference == "STG-PAID" ? Math.Round(service.Price * 0.1m, 0) : 0;
        var invoice = new Invoice
        {
            AppointmentId = appointment.AppointmentId,
            Subtotal = service.Price,
            DiscountAmount = discount,
            TotalAmount = service.Price - discount,
            PromotionCode = discount > 0 ? "DEMO10" : null,
            Status = "Paid",
            CreatedAt = completedOrSeededAt
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        db.Payments.Add(new Payment
        {
            InvoiceId = invoice.InvoiceId,
            Amount = invoice.TotalAmount,
            PaymentMethod = "Transfer",
            PaymentStatus = "Paid",
            PaidAt = completedOrSeededAt,
            TransactionCode = $"STG-{reference.Replace("STG-", string.Empty)}"
        });
        await db.SaveChangesAsync();
    }

    private static string NormalizeName(string value) => string.Concat(value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(character => char.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)));
}
