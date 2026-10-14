namespace SalonManagement.Models;

public class InvoiceAdjustment
{
    public int InvoiceAdjustmentId { get; set; }

    public int InvoiceId { get; set; }

    public int InvoiceLineId { get; set; }

    public int? StylistId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal AmountDelta { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    public string CreatedByName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public InvoiceLine InvoiceLine { get; set; } = null!;

    public Stylist? Stylist { get; set; }
}
