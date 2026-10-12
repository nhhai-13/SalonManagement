using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class CustomerProfileManagementService(ApplicationDbContext dbContext)
{
    private static readonly HashSet<string> VisitedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed"
    };

    private static readonly HashSet<string> NoShowStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "NoShow", "No Show"
    };

    public async Task<CustomerProfileResponse?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken);
        if (customer is null)
            return null;

        var appointmentStatuses = await dbContext.Appointments.AsNoTracking()
            .Where(appointment => appointment.CustomerId == customerId)
            .Select(appointment => new { appointment.AppointmentId, appointment.Status })
            .ToListAsync(cancellationToken);
        var uniqueAppointments = appointmentStatuses
            .GroupBy(appointment => appointment.AppointmentId)
            .Select(group => group.First());

        return new CustomerProfileResponse(
            customer.CustomerId,
            customer.FullName,
            customer.Phone,
            customer.Email,
            uniqueAppointments.Count(appointment => VisitedStatuses.Contains(appointment.Status)),
            uniqueAppointments.Count(appointment => NoShowStatuses.Contains(appointment.Status)),
            customer.Notes,
            customer.CreatedAt,
            customer.UpdatedAt);
    }

    public async Task<CustomerProfileResponse?> UpdateAsync(
        int customerId,
        UpdateCustomerProfileRequest request,
        string changedByUserId,
        string changedByEmail,
        CancellationToken cancellationToken = default)
    {
        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Họ tên khách không được để trống.", nameof(request));
        if (string.IsNullOrWhiteSpace(changedByUserId) || string.IsNullOrWhiteSpace(changedByEmail))
            throw new ArgumentException("Không xác định được tài khoản thực hiện thay đổi.", nameof(changedByUserId));

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken);
        if (customer is null)
            return null;

        var changed = !string.Equals(customer.FullName, fullName, StringComparison.Ordinal) ||
            !string.Equals(customer.Email, email, StringComparison.Ordinal) ||
            !string.Equals(customer.Notes, notes, StringComparison.Ordinal);
        if (changed)
        {
            dbContext.CustomerProfileChangeLogs.Add(new CustomerProfileChangeLog
            {
                CustomerId = customer.CustomerId,
                ChangedByUserId = changedByUserId,
                ChangedByEmail = changedByEmail.Trim(),
                PreviousFullName = customer.FullName,
                PreviousEmail = customer.Email,
                PreviousNotes = customer.Notes,
                UpdatedFullName = fullName,
                UpdatedEmail = email,
                UpdatedNotes = notes,
                ChangedAtUtc = DateTime.UtcNow
            });
            customer.FullName = fullName;
            customer.Email = email;
            customer.Notes = notes;
            customer.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetByIdAsync(customerId, cancellationToken);
    }
}
