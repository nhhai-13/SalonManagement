using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SalonManagement.Models.ViewModels;

public sealed class CreateStylistViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
    [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại phải gồm 10 chữ số và bắt đầu bằng số 0.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Giới thiệu không được vượt quá 500 ký tự.")]
    public string? Description { get; set; }

    public IFormFile? ProfileImage { get; set; }

    public List<int> ServiceIds { get; set; } = [];

    public IReadOnlyList<Service> AvailableServices { get; set; } = [];
}
