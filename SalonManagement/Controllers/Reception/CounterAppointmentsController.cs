using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[Route("reception/counter-appointments")]
public sealed class CounterAppointmentsController(ApplicationDbContext db, CustomerProfileService profiles) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Services = await db.Services.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.ServiceName).ToListAsync();
        ViewBag.Stylists = await db.Stylists.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.FullName).ToListAsync();
        return View();
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CounterAppointmentRequest request)
    {
        var name = request.FullName?.Trim(); var phone = BookingService.NormalizePhone(request.Phone); var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(name) || phone is null || request.ServiceIds is null || request.ServiceIds.Length == 0 || request.StylistId <= 0 || request.Date == default)
            return BadRequest(new { error = "Vui lòng nhập đầy đủ tên, số điện thoại, dịch vụ, thợ và giờ bắt đầu hợp lệ." });
        if (!string.IsNullOrWhiteSpace(email) && !System.Net.Mail.MailAddress.TryCreate(email, out _)) return BadRequest(new { error = "Email không đúng định dạng." });
        var services = await db.Services.Where(item => item.IsActive && request.ServiceIds.Contains(item.ServiceId)).ToListAsync();
        if (services.Count != request.ServiceIds.Distinct().Count()) return BadRequest(new { error = "Dịch vụ không hợp lệ." });
        var stylist = await db.Stylists.Include(item => item.Services).SingleOrDefaultAsync(item => item.StylistId == request.StylistId && item.IsActive);
        if (stylist is null) return BadRequest(new { error = "Thợ không hợp lệ." });
        var missing = services.Where(service => !stylist.Services.Any(skill => skill.ServiceId == service.ServiceId)).Select(service => service.ServiceName).ToList();
        if (missing.Count > 0) return BadRequest(new { error = "Thợ không thực hiện được: " + string.Join(", ", missing) });
        var existingCustomer = await profiles.FindByPhoneAsync(phone);
        var hasNameConflict = existingCustomer is not null && !CustomerProfileService.NamesEquivalent(existingCustomer.FullName, name);
        if (hasNameConflict && request.NameConflictResolution is not ("KeepProfile" or "UpdateProfile" or "UseForThisAppointment"))
            return Conflict(new { code = "name_conflict", error = "Tên nhập khác với tên trong hồ sơ. Vui lòng chọn cách xử lý.", profileName = existingCustomer!.FullName, enteredName = name });

        var resolution = hasNameConflict ? request.NameConflictResolution : null;
        var customer = existingCustomer is null
            ? await profiles.FindOrCreateAsync(name, phone, email)
            : resolution == "UpdateProfile"
                ? await profiles.FindOrCreateAsync(name, phone, email)
                : await profiles.FindOrCreateAsync(existingCustomer.FullName, phone, email, updateExistingName: false);
        var duration = services.Sum(item => item.DurationMinutes);
        var endTime = request.StartTime.Add(TimeSpan.FromMinutes(duration));
        var overlaps = await db.Appointments.AnyAsync(item => item.StylistId == request.StylistId && item.AppointmentDate == request.Date.Date && item.Status != "Cancelled" && item.Status != "Rejected" && item.Status != "NoShow" && item.StartTime < endTime && request.StartTime < item.EndTime);
        if (overlaps) return BadRequest(new { error = "Khung giờ đã chồng lấn với lịch khác của thợ." });
        db.Appointments.Add(new Appointment { Customer = customer, StylistId = request.StylistId, AppointmentDate = request.Date.Date, StartTime = request.StartTime, EndTime = endTime, Status = "Confirmed", BookingReference = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), BookingSource = "Counter", CustomerNameOverride = resolution == "UseForThisAppointment" ? name : null, NameConflictResolution = resolution, AppointmentServices = services.Select(item => new AppointmentService { ServiceId = item.ServiceId, Price = item.Price, DurationMinutes = item.DurationMinutes }).ToList() });
        await db.SaveChangesAsync();
        return Ok(new { message = "Đã tạo lịch hẹn tại quầy.", redirect = "/reception/schedule?date=" + request.Date.ToString("yyyy-MM-dd") });
    }

    [HttpGet("customer")]
    public async Task<IActionResult> Customer(string? phone)
    {
        var normalized = BookingService.NormalizePhone(phone);
        if (normalized is null) return BadRequest(new { error = "Số điện thoại không hợp lệ." });
        var customer = await db.Customers.AsNoTracking().Include(item => item.Appointments).ThenInclude(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .SingleOrDefaultAsync(item => item.Phone == normalized);
        if (customer is null) return Ok(new { found = false });
        var summary = await profiles.GetSummaryAsync(customer.CustomerId);
        var history = customer.Appointments.OrderByDescending(item => item.AppointmentDate).ThenByDescending(item => item.StartTime).Take(5)
            .Select(item => new { date = item.AppointmentDate.ToString("dd/MM/yyyy"), services = item.AppointmentServices.Select(service => service.Service.ServiceName), duration = item.AppointmentServices.Sum(service => service.DurationMinutes) });
        return Ok(new { found = true, customerId = customer.CustomerId, fullName = customer.FullName, email = customer.Email, profileUrl = "/reception/customers/" + customer.CustomerId, visitCount = summary?.VisitCount ?? 0, noShowCount = summary?.NoShowCount ?? 0, shopNotes = summary?.ShopNotes, history });
    }
}

public sealed record CounterAppointmentRequest(string? FullName, string? Phone, string? Email, int[]? ServiceIds, int StylistId, DateTime Date, TimeSpan StartTime, string? NameConflictResolution = null);
