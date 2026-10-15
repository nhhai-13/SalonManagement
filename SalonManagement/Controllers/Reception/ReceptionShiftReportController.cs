using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
[Route("reception/shift-report")]
public sealed class ReceptionShiftReportController(ApplicationDbContext db, TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? date)
    {
        var selected = (date ?? SalonClock.GetLocalNow(timeProvider)).Date;
        var paidPayments = (await db.Payments.AsNoTracking()
            .Include(item => item.Invoice).ThenInclude(item => item.Appointment).ThenInclude(item => item.Customer)
            .Include(item => item.Invoice).ThenInclude(item => item.Appointment).ThenInclude(item => item.Stylist)
            .Include(item => item.Invoice).ThenInclude(item => item.Appointment).ThenInclude(item => item.AppointmentServices).ThenInclude(item => item.Service)
            .Where(item => item.PaymentStatus == "Paid" && item.PaidAt != null)
            .ToListAsync())
            .Where(item => item.PaidAt!.Value.Date == selected)
            .OrderByDescending(item => item.PaidAt)
            .Select(item => new ReceptionShiftPaymentItem
            {
                CustomerName = item.Invoice.Appointment.CustomerNameOverride ?? item.Invoice.Appointment.Customer.FullName,
                StylistName = item.Invoice.Appointment.Stylist.FullName,
                Services = string.Join(", ", item.Invoice.Appointment.AppointmentServices.Select(service => service.Service.ServiceName)),
                Amount = item.Amount,
                Method = item.PaymentMethod,
                PaidAt = item.PaidAt!.Value
            }).ToList();
        return View(new ReceptionShiftReportViewModel { Date = selected, Payments = paidPayments, TotalCollected = paidPayments.Sum(item => item.Amount) });
    }
}
