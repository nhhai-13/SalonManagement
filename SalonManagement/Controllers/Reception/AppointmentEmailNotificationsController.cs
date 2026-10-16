using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using SalonManagement.Services;

namespace SalonManagement.Controllers.Reception;

[Authorize(Roles = UserRoles.Receptionist)]
public sealed class AppointmentEmailNotificationsPageController : Controller
{
    [HttpGet("reception/email-notifications")]
    public IActionResult Index() => View("~/Views/AppointmentEmailNotifications/Index.cshtml");
}

[Authorize(Roles = UserRoles.Receptionist)]
[ApiController]
[Route("api/reception/email-notifications")]
public sealed class AppointmentEmailNotificationsController(ApplicationDbContext db, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var now = SalonClock.GetLocalNow(timeProvider);
        var rows = await db.AppointmentEmailNotifications.AsNoTracking()
            .Include(item => item.Appointment).ThenInclude(item => item.Customer)
            .OrderByDescending(item => item.ScheduledAt).Take(150).ToListAsync();
        var manualCalls = rows.Where(item => item.NotificationType == "ManualCall" && item.Status == "ManualCallRequired")
            .Where(item => item.Appointment.AppointmentDate.Date.Add(item.Appointment.StartTime) <= now.AddHours(24))
            .Select(item => new ManualCallReminder(item.Appointment.BookingReference, item.Appointment.CustomerNameOverride ?? item.Appointment.Customer.FullName, item.Appointment.Customer.Phone, item.Appointment.AppointmentDate, item.Appointment.StartTime))
            .OrderBy(item => item.AppointmentDate).ThenBy(item => item.StartTime).ToList();
        return Ok(new AppointmentEmailNotificationSnapshot(manualCalls, rows.Select(item => new AppointmentEmailNotificationRow(
            item.Appointment.BookingReference, item.Appointment.CustomerNameOverride ?? item.Appointment.Customer.FullName,
            item.RecipientEmail, item.NotificationType, item.Status, item.AttemptCount, item.ScheduledAt, item.SentAt, item.CancelledAt, item.LastError)).ToList()));
    }
}

public sealed record AppointmentEmailNotificationSnapshot(IReadOnlyList<ManualCallReminder> ManualCalls, IReadOnlyList<AppointmentEmailNotificationRow> Notifications);
public sealed record ManualCallReminder(string Reference, string CustomerName, string Phone, DateTime AppointmentDate, TimeSpan StartTime);
public sealed record AppointmentEmailNotificationRow(string Reference, string CustomerName, string? Email, string NotificationType, string Status, int AttemptCount, DateTime ScheduledAt, DateTime? SentAt, DateTime? CancelledAt, string? LastError);
