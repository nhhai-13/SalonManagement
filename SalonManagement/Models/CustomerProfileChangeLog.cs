namespace SalonManagement.Models;

public sealed class CustomerProfileChangeLog
{
    public int CustomerProfileChangeLogId { get; set; }

    public int CustomerId { get; set; }

    public string ChangedByUserId { get; set; } = string.Empty;

    public string ChangedByEmail { get; set; } = string.Empty;

    public string PreviousFullName { get; set; } = string.Empty;

    public string? PreviousEmail { get; set; }

    public string? PreviousNotes { get; set; }

    public string UpdatedFullName { get; set; } = string.Empty;

    public string? UpdatedEmail { get; set; }

    public string? UpdatedNotes { get; set; }

    public DateTime ChangedAtUtc { get; set; }
}
