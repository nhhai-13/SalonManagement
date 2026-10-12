using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Collections.Concurrent;
using SalonManagement.Data;
using SalonManagement.Models;

namespace SalonManagement.Services;

public sealed class DuplicateCustomerPhoneException(string phone)
    : InvalidOperationException($"Có nhiều hồ sơ khách cùng số điện thoại {phone}; cần xử lý trùng trước khi đặt lịch.");

public sealed class CustomerProfileService(ApplicationDbContext dbContext)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PhoneLocks = new(StringComparer.Ordinal);

    public static bool AreEquivalentNames(string? first, string? second)
    {
        static string Normalize(string? name) => string.Join(' ', (name ?? string.Empty)
            .Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizePhone(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(character => character is >= '0' and <= '9').ToArray());
        return digits.StartsWith("84", StringComparison.Ordinal) && digits.Length >= 11
            ? "0" + digits[2..]
            : digits;
    }

    public static string NormalizeAndValidatePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || !HasValidPhoneCharacters(phone))
            throw new ArgumentException("Số điện thoại chỉ được chứa chữ số và dấu phân cách hợp lệ.", nameof(phone));

        var normalizedPhone = NormalizePhone(phone);
        if (normalizedPhone.Length != 10 || !normalizedPhone.StartsWith('0'))
            throw new ArgumentException("Vui lòng nhập số điện thoại hợp lệ.", nameof(phone));

        return normalizedPhone;
    }

    public async Task<ResolveBookingCustomerResponse> ResolveForBookingAsync(
        ResolveBookingCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("Vui lòng nhập họ tên khách.", nameof(request));
        var normalizedPhone = NormalizeAndValidatePhone(request.Phone);

        var phoneLock = PhoneLocks.GetOrAdd(normalizedPhone, _ => new SemaphoreSlim(1, 1));
        await phoneLock.WaitAsync(cancellationToken);
        IDbContextTransaction? transaction = null;
        try
        {
            if (dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null)
                transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var matches = await dbContext.Customers
                .Where(customer => customer.NormalizedPhone == normalizedPhone)
                .OrderBy(customer => customer.CustomerId)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (matches.Count > 1)
                throw new DuplicateCustomerPhoneException(request.Phone);

            if (matches.Count == 1)
            {
                var existing = matches[0];
                if (string.IsNullOrWhiteSpace(existing.Email) && !string.IsNullOrWhiteSpace(request.Email))
                {
                    existing.Email = request.Email.Trim();
                    existing.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return new ResolveBookingCustomerResponse(IsReturningCustomer: true, Created: false);
            }

            var customer = new Customer
            {
                FullName = request.FullName.Trim(),
                Phone = request.Phone.Trim(),
                NormalizedPhone = normalizedPhone,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim()
            };
            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new ResolveBookingCustomerResponse(IsReturningCustomer: false, Created: true);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
            phoneLock.Release();
        }
    }

    private static bool HasValidPhoneCharacters(string phone)
    {
        var firstNonWhitespace = Array.FindIndex(phone.ToCharArray(), character => !char.IsWhiteSpace(character));
        var plusCount = 0;
        for (var index = 0; index < phone.Length; index++)
        {
            var character = phone[index];
            if (char.IsAsciiDigit(character) || char.IsWhiteSpace(character) || character is '-' or '(' or ')' or '.' or '/')
                continue;
            if (character == '+' && index == firstNonWhitespace && ++plusCount == 1)
                continue;
            return false;
        }
        return true;
    }
}
