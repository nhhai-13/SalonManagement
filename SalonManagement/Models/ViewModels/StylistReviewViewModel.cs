using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models.ViewModels;

public sealed class StylistReviewViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;
    [Range(1, 5, ErrorMessage = "Chọn số sao từ 1 đến 5.")]
    public int Rating { get; set; }
    [StringLength(1000)]
    public string? Comment { get; set; }
    public string? StylistName { get; set; }
    public string? CustomerName { get; set; }
}
