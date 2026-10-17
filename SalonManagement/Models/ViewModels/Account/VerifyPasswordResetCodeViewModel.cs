using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models.ViewModels;

/// <summary>Thông tin mã OTP gửi về email trước khi cho phép đặt lại mật khẩu.</summary>
public class VerifyPasswordResetCodeViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã xác nhận.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã xác nhận gồm đúng 6 chữ số.")]
    [Display(Name = "Mã xác nhận")]
    public string Code { get; set; } = string.Empty;
}
