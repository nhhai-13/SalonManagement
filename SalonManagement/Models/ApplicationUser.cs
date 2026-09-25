using Microsoft.AspNetCore.Identity;

namespace SalonManagement.Models;

/// <summary>
/// Người dùng ứng dụng mở rộng từ IdentityUser,
/// hỗ trợ thu hồi refresh token sau khi đổi/đặt lại mật khẩu (AC3).
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Tài khoản có đang hoạt động không (Admin có thể vô hiệu hóa).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Danh sách Refresh Token của người dùng.</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
