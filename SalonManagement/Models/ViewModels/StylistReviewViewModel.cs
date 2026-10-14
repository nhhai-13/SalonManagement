using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models.ViewModels;

public sealed class StylistReviewViewModel
{
    public string? Token { get; set; }
    [StringLength(8)]
    public string? Reference { get; set; }
    [Range(1, 5, ErrorMessage = "Chọn số sao từ 1 đến 5.")]
    public int Rating { get; set; }
    [StringLength(500)]
    public string? Comment { get; set; }
    public string? StylistName { get; set; }
    public string? CustomerName { get; set; }
}
