using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models;

public class StagingEmailMessage
{
    public int StagingEmailMessageId { get; set; }
    [Required, StringLength(40)] public string Template { get; set; } = string.Empty;
    [Required, StringLength(256)] public string Recipient { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
