using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models.ViewModels;

/// <summary>ViewModel cho tính năng Đặt lại mật khẩu sau khi nhận email (AC2, AC3).</summary>
public class ResetPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Token được mã hóa base64url trong link email.</summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8,
        ErrorMessage = "Mật khẩu mới phải có ít nhất {2} ký tự.")]
    [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$",
        ErrorMessage = "Mật khẩu mới phải chứa cả chữ cái và chữ số.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới.")]
    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu mới")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
