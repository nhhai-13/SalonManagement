using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public class StylistReview
{
    public int StylistReviewId { get; set; }
    public int AppointmentId { get; set; }
    public int CustomerId { get; set; }
    public int StylistId { get; set; }
    [Required, StringLength(64)]
    public string ReviewTokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    [Range(1, 5)]
    public int? Rating { get; set; }
 codex/sprint-4-todo
    [StringLength(1000)]

    [StringLength(500)]
 develop
    public string? Comment { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public Stylist Stylist { get; set; } = null!;
}
