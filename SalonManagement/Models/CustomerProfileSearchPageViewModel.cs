using SalonManagement.Services;

namespace SalonManagement.Models;

public sealed class CustomerProfileSearchPageViewModel
{
    public string Query { get; init; } = string.Empty;
    public IReadOnlyList<CustomerProfileSearchResult> Results { get; init; } = [];
    public string? Message { get; init; }
}
