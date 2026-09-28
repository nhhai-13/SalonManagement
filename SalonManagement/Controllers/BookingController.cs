using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[ApiController]
[Route("api/booking")]
public sealed class BookingController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public BookingController(ApplicationDbContext db) => _db = db;

    [HttpGet("stylists")]
    public async Task<IActionResult> GetStylists([FromQuery] int serviceId)
    {
        var stylists = await _db.StylistServices.AsNoTracking()
            .Where(item => item.ServiceId == serviceId && item.Service.IsActive && item.Stylist.IsActive)
            .OrderBy(item => item.Stylist.FullName)
            .Select(item => new { item.Stylist.StylistId, item.Stylist.FullName })
            .ToListAsync();

        return Ok(stylists);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.CustomerName) ||
            string.IsNullOrWhiteSpace(request.Phone) || request.AppointmentDate.Date < DateTime.Today ||
            request.StartTime < TimeSpan.Zero || request.StartTime >= TimeSpan.FromDays(1))
        {
            return BadRequest(new { success = false, message = "Thông tin đặt lịch không hợp lệ." });
        }

        var service = await _db.Services.SingleOrDefaultAsync(item => item.ServiceId == request.ServiceId && item.IsActive);
        if (service is null)
            return BadRequest(new { success = false, message = "Dịch vụ không tồn tại hoặc đã ngừng cung cấp." });

        var stylist = await _db.Stylists.SingleOrDefaultAsync(item => item.StylistId == request.StylistId && item.IsActive);
        if (stylist is null)
            return BadRequest(new { success = false, message = "Thợ được chọn không tồn tại hoặc hiện không làm việc." });

        var assigned = await _db.StylistServices.AnyAsync(item =>
            item.StylistId == request.StylistId && item.ServiceId == request.ServiceId);
        if (!assigned)
            return BadRequest(new { success = false, message = "Thợ được chọn không cung cấp dịch vụ này." });

        var customer = await _db.Customers.FirstOrDefaultAsync(item => item.Phone == request.Phone.Trim());
        if (customer is null)
        {
            customer = new Customer { FullName = request.CustomerName.Trim(), Phone = request.Phone.Trim() };
            _db.Customers.Add(customer);
        }
        else
        {
            customer.FullName = request.CustomerName.Trim();
            customer.UpdatedAt = DateTime.Now;
        }

        var appointment = new Appointment
        {
            Customer = customer,
            StylistId = stylist.StylistId,
            AppointmentDate = request.AppointmentDate.Date,
            StartTime = request.StartTime,
            EndTime = request.StartTime.Add(TimeSpan.FromMinutes(service.DurationMinutes)),
            Status = "Pending",
            AppointmentServices =
            {
                new AppointmentService { ServiceId = service.ServiceId, Price = service.Price, DurationMinutes = service.DurationMinutes }
            }
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        return Ok(new { success = true, appointmentId = appointment.AppointmentId, message = "Đặt lịch thành công." });
    }
}

public sealed class CreateBookingRequest
{
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int ServiceId { get; set; }
    public int StylistId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
}
