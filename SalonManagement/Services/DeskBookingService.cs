using System.Data;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
namespace SalonManagement.Services;
public class DeskBookingService(ApplicationDbContext db, TimeProvider clock)
{
    public Task<Appointment> Create(DeskBookingRequest request, string actor)
    {
        var attempts = 0;
        return db.Database.CreateExecutionStrategy().ExecuteAsync(() =>
        {
            if (attempts++ > 0) db.ChangeTracker.Clear();
            return CreateCore(request, actor);
        });
    }
    private async Task<Appointment> CreateCore(DeskBookingRequest request, string actor)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        if (string.IsNullOrWhiteSpace(actor)) throw new InvalidOperationException("Không xác định được người thực hiện.");
        var start = DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Unspecified);
        var now = clock.GetUtcNow();
        if (new DateTimeOffset(start, TimeSpan.FromHours(7)) < now) throw new InvalidOperationException("Không thể đặt lịch trong quá khứ.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var stylist = await db.Stylists.SingleOrDefaultAsync(s => s.StylistId == request.StylistId && s.IsActive)
            ?? throw new InvalidOperationException("Thợ không hoạt động.");
        // Serialize bookings and undo on this stylist, including previously released slots.
        if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT StylistId FROM Stylists WITH (UPDLOCK, HOLDLOCK) WHERE StylistId = {stylist.StylistId}");
        var service = await db.Services.SingleOrDefaultAsync(s => s.ServiceId == request.ServiceId && s.IsActive)
            ?? throw new InvalidOperationException("Dịch vụ không hoạt động.");
        if (!await db.StylistServices.AnyAsync(link => link.StylistId == stylist.StylistId && link.ServiceId == service.ServiceId))
            throw new InvalidOperationException("Thợ chưa được gán dịch vụ đã chọn.");
        if (service.DurationMinutes <= 0) throw new InvalidOperationException("Thời lượng dịch vụ không hợp lệ.");
        var end = start.AddMinutes(service.DurationMinutes);
        if (end.Date != start.Date) throw new InvalidOperationException("Lịch hẹn phải kết thúc trong ngày.");
        var shifts = await db.WorkSchedules.Where(s => s.StylistId == stylist.StylistId && s.WorkDate >= start.Date &&
            s.WorkDate < start.Date.AddDays(1) && s.Status == "Working").ToListAsync();
        if (!shifts.Any(s => s.StartTime <= start.TimeOfDay && s.EndTime >= end.TimeOfDay))
            throw new InvalidOperationException("Khung giờ phải nằm trong ca làm của thợ.");
        var hours = await db.BusinessHours.SingleOrDefaultAsync(h => h.DayOfWeek == start.DayOfWeek);
        if (hours != null && (hours.IsClosed || hours.OpensAt == null || hours.ClosesAt == null ||
            start.TimeOfDay < hours.OpensAt.Value.ToTimeSpan() || end.TimeOfDay > hours.ClosesAt.Value.ToTimeSpan()))
            throw new InvalidOperationException("Khung giờ nằm ngoài giờ mở cửa.");
        var occupied = await db.Appointments.Where(a => a.StylistId == stylist.StylistId && a.AppointmentDate >= start.Date &&
            a.AppointmentDate < start.Date.AddDays(1) && a.Status != "NoShow" && a.Status != "Cancelled").ToListAsync();
        if (occupied.Any(a => a.StartTime < end.TimeOfDay && a.EndTime > start.TimeOfDay))
            throw new InvalidOperationException("Khung giờ đã có lịch hẹn khác.");
        var count = await db.Appointments.CountAsync(a => a.Customer.Phone == request.Phone && a.Status == "NoShow");
        if (count > 0 && !request.AcknowledgeNoShow)
            throw new InvalidOperationException($"Khách đã không đến {count} lần. Vui lòng xác nhận đã xem cảnh báo.");
        var customer = await db.Customers.OrderBy(c => c.CustomerId).FirstOrDefaultAsync(c => c.Phone == request.Phone);
        customer ??= new Customer { FullName = request.FullName.Trim(), Phone = request.Phone, CreatedAt = now.UtcDateTime };
        var appointment = new Appointment { Customer = customer, StylistId = stylist.StylistId,
            AppointmentDate = start.Date, StartTime = start.TimeOfDay, EndTime = end.TimeOfDay, Status = "Confirmed", CreatedAt = now.UtcDateTime };
        appointment.AppointmentServices.Add(new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes });
        db.Add(appointment); await db.SaveChangesAsync();
        db.AppointmentAudits.Add(new() { AppointmentId = appointment.AppointmentId, ActorId = actor, Action = "desk-booking", PreviousStatus = "", NewStatus = appointment.Status, OccurredAt = now });
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return appointment;
    }
}
