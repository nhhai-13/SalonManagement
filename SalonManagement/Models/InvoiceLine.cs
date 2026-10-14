namespace SalonManagement.Models;

public class InvoiceLine
{
    public int InvoiceLineId { get; set; }

    public int InvoiceId { get; set; }

    public int AppointmentServiceId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public Invoice Invoice { get; set; } = null!;

    public AppointmentService AppointmentService { get; set; } = null!;
}
