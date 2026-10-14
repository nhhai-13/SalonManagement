using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public class RehearsalRun
{
    public int RehearsalRunId { get; set; }
    [Required, StringLength(256)]
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    [StringLength(1000)]
    public string? Notes { get; set; }
    public ICollection<RehearsalIssue> Issues { get; set; } = new List<RehearsalIssue>();
}

public class RehearsalIssue
{
    public int RehearsalIssueId { get; set; }
    public int RehearsalRunId { get; set; }
    [Required, StringLength(200)]
    public string Step { get; set; } = string.Empty;
    [Required, StringLength(128)]
    public string Account { get; set; } = string.Empty;
    [Required, StringLength(2000)]
    public string Observation { get; set; } = string.Empty;
    [Required, StringLength(16)]
    public string Severity { get; set; } = "Normal";
    [Required, StringLength(24)]
    public string Status { get; set; } = "New";
    [StringLength(256)]
    public string? Assignee { get; set; }
    [StringLength(256)]
    public string? LastChangedBy { get; set; }
    public DateTime? LastChangedAt { get; set; }
    public RehearsalRun Run { get; set; } = null!;
}
