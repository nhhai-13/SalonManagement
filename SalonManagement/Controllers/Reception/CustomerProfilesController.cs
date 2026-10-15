using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist + "," + UserRoles.Owner)]
[Route("reception/customers")]
public sealed class CustomerProfilesController(ApplicationDbContext db, CustomerProfileService profiles) : Controller
{
    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> Details(int customerId)
    {
        var customer = await db.Customers.AsNoTracking().Include(item => item.Appointments).ThenInclude(item => item.Stylist)
            .Include(item => item.Appointments).ThenInclude(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .SingleOrDefaultAsync(item => item.CustomerId == customerId);
        if (customer is null) return NotFound();
        db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = CurrentUserId(),
            UserName = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name,
            Action = "VIEW",
            EntityType = nameof(Customer),
            EntityId = customerId.ToString(),
            Changes = "Xem hồ sơ khách hàng"
        });
        await db.SaveChangesAsync();
        var summary = await profiles.GetSummaryAsync(customerId);
        return View(new CustomerProfileDetailsViewModel
        {
            Customer = customer,
            VisitCount = summary?.VisitCount ?? 0,
            NoShowCount = summary?.NoShowCount ?? 0,
            ShopNotes = summary?.ShopNotes
        });
    }

    [HttpPut("{customerId:int}/shop-notes")]
    public async Task<IActionResult> SaveShopNotes(int customerId, [FromBody] CustomerShopNotesRequest request)
    {
        try
        {
            await profiles.SaveShopNotesAsync(customerId, request.Notes);
            return Ok(new { message = "Đã lưu ghi chú riêng của tiệm." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private string? CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
}

public sealed record CustomerShopNotesRequest(string? Notes);
