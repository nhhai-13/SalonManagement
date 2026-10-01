using SalonManagement.Models;

namespace SalonManagement.Services;

public interface IStaffAccountService
{
    Task<StaffAccountListResponse> GetAccountsAsync(string? search, string? role, bool? isActive);
    Task<(bool Success, string Message)> UpdateAsync(string id, UpdateStaffAccountRequest request);
    Task<(bool Success, string Message)> ChangeStatusAsync(string id, bool isActive);
    Task<(bool Success, string Message)> UnlockAsync(string id);
    Task<(bool Success, string Message)> RevokeSessionsAsync(string id);
}
