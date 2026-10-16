using Microsoft.AspNetCore.Identity;

namespace SalonManagement.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public int? StylistId { get; set; }
    public Stylist? Stylist { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public string? EmailVerificationCodeHash { get; set; }
    public DateTime? EmailVerificationCodeExpiresAtUtc { get; set; }
    public DateTime? EmailVerificationCodeSentAtUtc { get; set; }
    public int EmailVerificationFailedAttempts { get; set; }
    // Vai trò mà nhân viên đã chọn khi tự đăng ký. Đây chưa phải là quyền truy cập
    // cho đến khi quản trị viên hoặc chủ tiệm phê duyệt yêu cầu.
    public string? RequestedRole { get; set; }
    public DateTime? AccessRequestedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<StaffWorkSchedule> StaffWorkSchedules { get; set; } = new List<StaffWorkSchedule>();
}
