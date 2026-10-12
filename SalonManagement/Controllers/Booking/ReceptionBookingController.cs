using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers.Booking;

[ApiController]
[Authorize(Roles = UserRoles.Receptionist)]
[Route("api/reception/bookings")]
public class ReceptionBookingController(
    ApplicationDbContext db) : Controller
{
    [HttpGet("/reception/booking/create")]
    public IActionResult CreatePage()
    {
    return View("~/Views/ReceptionBooking/Create.cshtml");
    }
    // Tìm khách quen theo số điện thoại
    [HttpGet("customer-by-phone")]
    public async Task<IActionResult> GetCustomerByPhone(
        [FromQuery] string? phone)
{
    phone = phone?.Trim();

    if (string.IsNullOrWhiteSpace(phone) ||
        !Regex.IsMatch(phone, @"^0[35789]\d{8}$"))
    {
        return BadRequest(new
        {
            message = "Số điện thoại không hợp lệ."
        });
    }

    var customer = await db.Customers
        .AsNoTracking()
        .Where(c => c.Phone == phone)
        .Select(c => new
        {
            c.CustomerId,
            c.FullName,
            c.Phone
        })
        .FirstOrDefaultAsync();

    if (customer == null)
    {
        return Ok(new
        {
            found = false,
            message = "Chưa có khách hàng với số điện thoại này."
        });
    }

    var history = await db.AppointmentServices
        .AsNoTracking()
        .Where(aps => aps.Appointment.CustomerId == customer.CustomerId)
        .OrderByDescending(aps => aps.Appointment.AppointmentDate)
        .ThenByDescending(aps => aps.Appointment.StartTime)
        .Select(aps => new
        {
            aps.AppointmentId,
            aps.Appointment.AppointmentDate,
            aps.Appointment.StartTime,
            ServiceName = aps.Service.ServiceName,
            aps.DurationMinutes
        })
        .Take(5)
        .ToListAsync();

    return Ok(new
    {
        found = true,
        customer,
        history
    });
    }
    [HttpGet("services")]
    public async Task<IActionResult> Services()
    {
        var services = await db.Services
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.ServiceName)
            .Select(s => new
            {
                s.ServiceId,
                s.ServiceName,
                s.Price,
                s.DurationMinutes
            })
            .ToListAsync();

        return Ok(services);
    }
    [HttpGet("stylists")]
    public async Task<IActionResult> Stylists()
    {
    var stylists = await db.Stylists
        .AsNoTracking()
        .Where(s => s.IsActive)
        .OrderBy(s => s.FullName)
        .Select(s => new
        {
            s.StylistId,
            s.FullName
        })
        .ToListAsync();

    return Ok(stylists);
    }
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateReceptionBookingRequest request)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var name = request.CustomerName.Trim();
        var phone = request.Phone.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new
            {
                message = "Vui lòng nhập tên khách hàng."
            });

        if (!Regex.IsMatch(phone, @"^0[35789]\d{8}$"))
            return BadRequest(new
            {
                message = "Số điện thoại không hợp lệ."
            });

        if (request.Date == default)
            return BadRequest(new
            {
                message = "Vui lòng chọn ngày đặt lịch."
            });

        var service = await db.Services
            .FirstOrDefaultAsync(s =>
                s.ServiceId == request.ServiceId && s.IsActive);

        if (service == null)
            return BadRequest(new
            {
                message = "Dịch vụ không tồn tại hoặc đã ngừng hoạt động."
            });

        if (service.DurationMinutes <= 0)
            return BadRequest(new
            {
                message = "Thời lượng dịch vụ không hợp lệ."
            });

        var stylistExists = await db.Stylists
            .AnyAsync(s =>
                s.StylistId == request.StylistId && s.IsActive);

        if (!stylistExists)
            return BadRequest(new
            {
                message = "Thợ không tồn tại hoặc đã ngừng hoạt động."
            });

        var start = request.StartTime.ToTimeSpan();
        var end = start.Add(
            TimeSpan.FromMinutes(service.DurationMinutes));

        var date = request.Date.ToDateTime(TimeOnly.MinValue);

        if (end > TimeSpan.FromDays(1))
            return BadRequest(new
            {
                message = "Giờ kết thúc vượt quá ngày đặt lịch."
            });

        if (date.Add(start) <= DateTime.Now)
            return BadRequest(new
            {
                message = "Thời gian đặt lịch phải ở tương lai."
            });

        await using var transaction =
            await db.Database.BeginTransactionAsync();

    // Nhận diện khách quen theo số điện thoại
    var customer = await db.Customers
        .FirstOrDefaultAsync(c => c.Phone == phone);

    if (customer == null)
    {
        // Khách mới: tạo hồ sơ khách hàng
        customer = new Customer
    {
        FullName = name,
        Phone = phone,
        IsActive = true
    };

    db.Customers.Add(customer);
}
else
{
    // Khách quen: sử dụng lại hồ sơ đã có
    // Không tạo thêm Customer mới
}

        var appointment = new Appointment
        {
            Customer = customer,
            StylistId = request.StylistId,
            AppointmentDate = date,
            StartTime = start,
            EndTime = end,
            Status = "Pending",
            AppointmentServices = new List<AppointmentService>
            {
                new()
                {
                    ServiceId = service.ServiceId,
                    Price = service.Price,
                    DurationMinutes = service.DurationMinutes
                }
            }
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            message = "Tạo lịch tại quầy thành công.",
            appointment.AppointmentId,
            customer.CustomerId,
            appointment.StylistId,
            appointment.AppointmentDate,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Status
        });
    }
}

public class CreateReceptionBookingRequest
{
    [Required]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ServiceId { get; set; }

    [Range(1, int.MaxValue)]
    public int StylistId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }
}