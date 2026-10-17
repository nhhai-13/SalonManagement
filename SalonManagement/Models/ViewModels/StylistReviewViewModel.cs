using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models.ViewModels;

public sealed class StylistReviewViewModel
{
    public string? Token { get; set; }

    [StringLength(8)]
    public string? Reference { get; set; }

    [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5.")]
    public int Rating { get; set; }

    [StringLength(500, ErrorMessage = "Góp ý không được vượt quá 500 ký tự.")]
    public string? Comment { get; set; }

    public string? StylistName { get; set; }
    public string? CustomerName { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public string? FormattedDate { get; set; }
    public List<string> Services { get; set; } = new();

    public bool IsAlreadyReviewed { get; set; }
}

