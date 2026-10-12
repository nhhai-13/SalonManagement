using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;
using SalonManagement.Models;
using System.Globalization;
using System.Text;

namespace SalonManagement.Services;

public sealed record CustomerConsolidationResult(int ConsolidatedProfiles, int ReassignedAppointments, int SkippedWithoutValidPhone);
public sealed record CustomerProfileSummary(int VisitCount, int NoShowCount, string? ShopNotes);

public sealed class CustomerProfileService(ApplicationDbContext db)
{
    public const int ShopNotesMaxLength = 500;

    public Task<Customer?> FindByPhoneAsync(string? phone) =>
        BookingService.NormalizePhone(phone) is { } normalized
            ? db.Customers.FirstOrDefaultAsync(customer => customer.Phone == normalized)
            : Task.FromResult<Customer?>(null);

    public async Task<Customer> FindOrCreateAsync(string fullName, string phone, string? email, bool updateExistingName = true, bool overwriteExistingEmail = true)
    {
        var normalized = BookingService.NormalizePhone(phone) ?? throw new ArgumentException("Số điện thoại không hợp lệ.");
        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Phone == normalized);
        if (customer is not null)
        {
            if (updateExistingName) customer.FullName = fullName.Trim();
            customer.NormalizedSearchName = NormalizeSearchName(customer.FullName);
            if (!string.IsNullOrWhiteSpace(email) && (overwriteExistingEmail || string.IsNullOrWhiteSpace(customer.Email))) customer.Email = email.Trim();
            customer.UpdatedAt = DateTime.Now;
            return customer;
        }
        customer = new Customer { FullName = fullName.Trim(), NormalizedSearchName = NormalizeSearchName(fullName), Phone = normalized, Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim() };
        db.Customers.Add(customer);
        return customer;
    }

    public async Task<CustomerProfileSummary?> GetSummaryAsync(int customerId)
    {
        var customer = await db.Customers.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .Select(item => new { item.Notes })
            .SingleOrDefaultAsync();
        if (customer is null) return null;

        var counts = await db.Appointments.AsNoTracking()
            .Where(item => item.CustomerId == customerId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Visits = group.Count(item => item.Status == "Arrived" || item.Status == "InProgress" || item.Status == "Completed"),
                NoShows = group.Count(item => item.Status == "NoShow")
            })
            .SingleOrDefaultAsync();

        return new CustomerProfileSummary(counts?.Visits ?? 0, counts?.NoShows ?? 0, customer.Notes);
    }

    public async Task SaveShopNotesAsync(int customerId, string? notes)
    {
        var normalized = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalized?.Length > ShopNotesMaxLength)
            throw new ArgumentException($"Ghi chú tối đa {ShopNotesMaxLength} ký tự.", nameof(notes));

        var customer = await db.Customers.SingleOrDefaultAsync(item => item.CustomerId == customerId)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ khách hàng.");
        customer.Notes = normalized;
        customer.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task<CustomerConsolidationResult> ConsolidateLegacyProfilesAsync()
    {
        var customers = await db.Customers.Include(customer => customer.Appointments).ToListAsync();
        var groups = customers.Select(customer => new { Customer = customer, Phone = BookingService.NormalizePhone(customer.Phone) })
            .Where(item => item.Phone is not null).GroupBy(item => item.Phone!, StringComparer.Ordinal);
        var consolidated = 0; var reassigned = 0;
        foreach (var group in groups)
        {
            var ordered = group.OrderByDescending(item => item.Customer.Appointments.Max(appointment => (DateTime?)appointment.AppointmentDate) ?? DateTime.MinValue)
                .ThenByDescending(item => item.Customer.CustomerId).ToList();
            var canonical = ordered[0].Customer;
            canonical.Phone = group.Key;
            canonical.FullName = canonical.FullName.Trim();
            canonical.NormalizedSearchName = NormalizeSearchName(canonical.FullName);
            canonical.Email ??= ordered.Select(item => item.Customer.Email).FirstOrDefault(email => !string.IsNullOrWhiteSpace(email));
            foreach (var duplicate in ordered.Skip(1))
            {
                var appointments = await db.Appointments.Where(appointment => appointment.CustomerId == duplicate.Customer.CustomerId).ToListAsync();
                foreach (var appointment in appointments) { appointment.CustomerId = canonical.CustomerId; reassigned++; }
                db.Customers.Remove(duplicate.Customer);
                consolidated++;
            }
        }
        var skipped = customers.Count(customer => BookingService.NormalizePhone(customer.Phone) is null);
        if (consolidated > 0 || customers.Any(customer => BookingService.NormalizePhone(customer.Phone) != customer.Phone)) await db.SaveChangesAsync();
        return new(consolidated, reassigned, skipped);
    }

    public async Task<int> RebuildSearchNamesAsync()
    {
        var customers = await db.Customers.ToListAsync();
        var changed = 0;
        foreach (var customer in customers)
        {
            var normalized = NormalizeSearchName(customer.FullName);
            if (customer.NormalizedSearchName == normalized) continue;
            customer.NormalizedSearchName = normalized;
            changed++;
        }
        if (changed > 0) await db.SaveChangesAsync();
        return changed;
    }

    public static string NormalizeSearchName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = new StringBuilder(value.Length);
        foreach (var character in value.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(character);
            normalized.Append(lower == 'đ' ? 'd' : lower);
        }
        return normalized.ToString();
    }

    public static bool NamesEquivalent(string? left, string? right) =>
        string.Equals(NormalizeComparisonName(left), NormalizeComparisonName(right), StringComparison.Ordinal);

    private static string NormalizeComparisonName(string? value) =>
        string.Join(' ', NormalizeSearchName(value).Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
