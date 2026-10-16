namespace SalonManagement.Models;

/// <summary>
/// A single-use customer review invitation tied to one completed appointment.
/// The public token is never persisted; only its SHA-256 hash is stored.
/// </summary>
public sealed class ServiceReview
{
    public int ServiceReviewId { get; set; }
    public int AppointmentId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? Rating { get; set; }
    public string? Comment { get; set; }
    public Appointment Appointment { get; set; } = null!;
}
