using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class CustomerProfileManagementService(ApplicationDbContext dbContext)
{
    private const int MaxSearchResults = 25;
    private static readonly HashSet<string> VisitedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Completed"
    };

    private static readonly HashSet<string> NoShowStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "NoShow", "No Show"
    };

    public async Task<CustomerSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = NormalizeName(query);
        var phoneQuery = NormalizePhoneQuery(query);
        if (normalizedQuery.Length < 2 && phoneQuery.Length < 2)
            throw new ArgumentException("Vui lòng nhập ít nhất 2 ký tự để tìm hồ sơ khách.", nameof(query));
        if (normalizedQuery.Length > 100)
            throw new ArgumentException("Từ khóa tìm kiếm không được vượt quá 100 ký tự.", nameof(query));

        var customers = await dbContext.Customers.AsNoTracking()
            .Where(customer => customer.IsActive)
            .Select(customer => new
            {
                customer.CustomerId,
                customer.FullName,
                customer.Phone,
                customer.NormalizedPhone,
                customer.Email
            })
            .ToListAsync(cancellationToken);

        var matching = customers.Select(customer =>
        {
            var name = NormalizeName(customer.FullName);
            var phone = NormalizePhoneQuery(string.IsNullOrWhiteSpace(customer.NormalizedPhone)
                ? customer.Phone
                : customer.NormalizedPhone);
            var phoneMatches = phoneQuery.Length >= 2 && phone.Contains(phoneQuery, StringComparison.Ordinal);
            var nameMatches = normalizedQuery.Length >= 2 && name.Contains(normalizedQuery, StringComparison.Ordinal);
            var exact = name.Equals(normalizedQuery, StringComparison.Ordinal) ||
                (phoneQuery.Length >= 2 && phone.Equals(phoneQuery, StringComparison.Ordinal));
            var prefix = name.StartsWith(normalizedQuery, StringComparison.Ordinal) ||
                (phoneQuery.Length >= 2 && phone.StartsWith(phoneQuery, StringComparison.Ordinal));
            return new { Customer = customer, Name = name, Matches = nameMatches || phoneMatches, Rank = exact ? 0 : prefix ? 1 : 2 };
        })
        .Where(item => item.Matches)
        .OrderBy(item => item.Rank)
        .ThenBy(item => item.Name, StringComparer.Ordinal)
        .ThenBy(item => item.Customer.CustomerId)
        .ToList();

        var page = matching.Take(MaxSearchResults).ToList();
        var ids = page.Select(item => item.Customer.CustomerId).ToArray();
        var completedAppointments = await dbContext.Appointments.AsNoTracking()
            .Where(appointment => ids.Contains(appointment.CustomerId))
            .Select(appointment => new { appointment.CustomerId, appointment.AppointmentId, appointment.Status })
            .ToListAsync(cancellationToken);
        var visitCounts = completedAppointments
            .Where(appointment => VisitedStatuses.Contains(appointment.Status))
            .GroupBy(appointment => appointment.CustomerId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.AppointmentId).Distinct().Count());

        var results = page.Select(item => new CustomerSearchResult(
            item.Customer.CustomerId, item.Customer.FullName, item.Customer.Phone,
            item.Customer.Email, visitCounts.GetValueOrDefault(item.Customer.CustomerId))).ToArray();
        return new CustomerSearchResponse(results, matching.Count, matching.Count > MaxSearchResults);
    }

    private static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character == 'đ' ? 'd' : character == 'Đ' ? 'D' : character);
        }
        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizePhoneQuery(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.StartsWith("84", StringComparison.Ordinal) ? "0" + digits[2..] : digits;
    }

    public async Task<CustomerProfileResponse?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken);
        if (customer is null)
            return null;

        return await BuildResponseAsync(customer, cancellationToken);
    }

    public async Task<CustomerPhoneLookupResponse> LookupByPhoneAsync(
        string phone,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = CustomerProfileService.NormalizeAndValidatePhone(phone);
        var matches = await dbContext.Customers.AsNoTracking()
            .Where(customer => customer.NormalizedPhone == normalizedPhone)
            .OrderBy(customer => customer.CustomerId)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (matches.Count > 1)
            throw new DuplicateCustomerPhoneException(phone);
        if (matches.Count == 0)
            return new CustomerPhoneLookupResponse(false, null);

        return new CustomerPhoneLookupResponse(true, await BuildResponseAsync(matches[0], cancellationToken));
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

    private async Task<CustomerProfileResponse> BuildResponseAsync(Customer customer, CancellationToken cancellationToken)
    {
        var appointmentStatuses = await dbContext.Appointments.AsNoTracking()
            .Where(appointment => appointment.CustomerId == customer.CustomerId)
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
}
