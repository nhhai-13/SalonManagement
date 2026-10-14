using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public static class AppointmentEmailKinds
{
    public const string Confirmation = "Confirmation";
    public const string Reminder = "Reminder";
}

public static class AppointmentEmailStatuses
{
    public const string Queued = "Queued";
    public const string Retrying = "Retrying";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string NoEmail = "NoEmail";
}

public sealed class AppointmentEmailDelivery
{
    public int AppointmentEmailDeliveryId { get; set; }
    public int AppointmentId { get; set; }
    [Required, StringLength(20)] public string Kind { get; set; } = AppointmentEmailKinds.Confirmation;
    [StringLength(256)] public string? RecipientEmail { get; set; }
    [Required, StringLength(20)] public string Status { get; set; } = AppointmentEmailStatuses.Queued;
    public DateTimeOffset AppointmentStartAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    [StringLength(1000)] public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Appointment Appointment { get; set; } = null!;
}
