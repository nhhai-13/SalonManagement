using Microsoft.EntityFrameworkCore;
using SalonManagement.Data;

namespace SalonManagement.Services;

public sealed record CustomerProfileSearchResult(int CustomerId, string FullName, string Phone, int VisitCount, int NoShowCount);
public sealed record CustomerProfileSearchResponse(IReadOnlyList<CustomerProfileSearchResult> Results, string? Error = null);

public sealed class CustomerProfileSearchService(ApplicationDbContext db)
{
    public const int MinimumNameLength = 2;
    public const int MinimumPhoneDigits = 4;
    public const int MaximumResults = 20;

    public async Task<CustomerProfileSearchResponse> SearchAsync(string? keyword)
    {
        var input = keyword?.Trim() ?? string.Empty;
        if (input.Length == 0) return new([], "Nhập tên khách hoặc ít nhất 4 số cuối điện thoại.");

        var isPhoneSearch = input.All(character => char.IsDigit(character) || character is '+' or ' ' or '.' or '-' or '(' or ')');
        IQueryable<SalonManagement.Models.Customer> customers = db.Customers.AsNoTracking();
        if (isPhoneSearch)
        {
            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length < MinimumPhoneDigits)
                return new([], $"Nhập ít nhất {MinimumPhoneDigits} chữ số để tìm theo số điện thoại.");
            var exactPhone = BookingService.NormalizePhone(input);
            customers = exactPhone is not null
                ? customers.Where(customer => customer.Phone == exactPhone)
                : customers.Where(customer => customer.Phone.Contains(digits));
        }
        else
        {
            var normalizedName = CustomerProfileService.NormalizeSearchName(input);
            if (normalizedName.Length < MinimumNameLength)
                return new([], $"Nhập ít nhất {MinimumNameLength} ký tự để tìm theo tên.");
            customers = customers.Where(customer => customer.NormalizedSearchName.Contains(normalizedName));
        }

        var rows = await customers
            .OrderByDescending(customer => customer.UpdatedAt ?? customer.CreatedAt)
            .ThenBy(customer => customer.FullName)
            .Select(customer => new
            {
                customer.CustomerId,
                customer.FullName,
                customer.Phone,
                VisitCount = customer.Appointments.Count(appointment => appointment.Status == "Arrived" || appointment.Status == "InProgress" || appointment.Status == "Completed"),
                NoShowCount = customer.Appointments.Count(appointment => appointment.Status == "NoShow")
            })
            .Take(MaximumResults)
            .ToListAsync();

        return new(rows.Select(row => new CustomerProfileSearchResult(row.CustomerId, row.FullName, row.Phone, row.VisitCount, row.NoShowCount)).ToList());
    }
}
