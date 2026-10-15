using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Controllers;

[ApiController]
[Authorize(Roles = RoleGroups.FrontDesk)]
[Route("api/reception/customers")]
public sealed class ReceptionCustomerDirectoryController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var value = query?.Trim() ?? string.Empty;
        if (value.Length < 2) return Ok(Array.Empty<CustomerProfileResult>());
        var phone = new string(value.Where(char.IsDigit).ToArray());
        var customers = await db.Customers.AsNoTracking()
            .Where(customer => (phone.Length > 0 && customer.Phone.StartsWith(phone)) || customer.FullName.StartsWith(value))
            .OrderBy(customer => phone.Length > 0 && customer.Phone == phone ? 0 : 1)
            .ThenBy(customer => customer.FullName)
            .Take(25)
            .Select(customer => new CustomerProfileResult(
                customer.CustomerId,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.Notes,
                customer.Appointments.Count(appointment => appointment.Status == "Completed"),
                customer.Appointments.Count(appointment => appointment.Status == AppointmentStatuses.NoShow)))
            .ToListAsync(cancellationToken);
        return Ok(customers);
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string? phone, CancellationToken cancellationToken)
    {
        var normalized = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalized.Length != 10 || !normalized.StartsWith('0'))
            return BadRequest(new { message = "Số điện thoại phải có 10 chữ số Việt Nam." });
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(item => item.Phone == normalized, cancellationToken);
        if (customer is null) return Ok(new { found = false });
        var history = await db.Appointments.AsNoTracking()
            .Where(item => item.CustomerId == customer.CustomerId && item.Status == "Completed")
            .OrderByDescending(item => item.AppointmentDate).ThenByDescending(item => item.StartTime)
            .Take(5)
            .Select(item => new
            {
                item.AppointmentDate,
                item.Stylist.FullName,
                Services = item.AppointmentServices.Select(service => service.Service.ServiceName).ToList()
            }).ToListAsync(cancellationToken);
        var totalVisits = await db.Appointments.CountAsync(item => item.CustomerId == customer.CustomerId && item.Status == "Completed", cancellationToken);
        var noShows = await db.Appointments.CountAsync(item => item.CustomerId == customer.CustomerId && item.Status == AppointmentStatuses.NoShow, cancellationToken);
        return Ok(new
        {
            found = true,
            customer.CustomerId,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.Notes,
            totalVisits,
            noShowCount = noShows,
            history
        });
    }

    [HttpPut("{customerId:int}/notes")]
    public async Task<IActionResult> UpdateNotes(int customerId, [FromBody] UpdateCustomerNotesRequest request, CancellationToken cancellationToken)
    {
        if (request.Notes?.Length > 1000) return BadRequest(new { message = "Ghi chú tối đa 1.000 ký tự." });
        var customer = await db.Customers.SingleOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken);
        if (customer is null) return NotFound(new { message = "Không tìm thấy hồ sơ khách." });
        customer.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { customer.CustomerId, customer.Notes, customer.UpdatedAt });
    }
}

public sealed record CustomerProfileResult(int CustomerId, string FullName, string Phone, string? Email, string? Notes, int TotalVisits, int NoShowCount);
public sealed record UpdateCustomerNotesRequest(string? Notes);
