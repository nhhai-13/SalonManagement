using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models.ViewModels;

/// <summary>ViewModel cho tính năng Quên mật khẩu — bước nhập email (AC2, AC4).</summary>
public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email.")]
    [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
