using System.ComponentModel.DataAnnotations;

namespace SalonManagement.Models.ViewModels;

public class InvoiceDetailsViewModel
{
    public Invoice Invoice { get; set; } = null!;
    public IReadOnlyList<Service> AvailableServices { get; set; } = [];
    public bool CanClose { get; set; }
    public IReadOnlyList<string> CloseBlockers { get; set; } = [];
    public decimal AdjustedTotal { get; set; }
    public StylistReview? StylistReview { get; set; }
    public string? ReviewInvitationStatus { get; set; }
    public string? ReviewLink { get; set; }
}

public class InvoiceDayViewModel
{
    public DateTime SelectedDate { get; init; }
    public IReadOnlyList<Appointment> Appointments { get; init; } = [];
    public IReadOnlyList<Invoice> PaidInvoices { get; init; } = [];
    public decimal SubtotalTotal => PaidInvoices.Sum(item => item.Subtotal);
    public decimal DiscountTotal => PaidInvoices.Sum(item => item.DiscountAmount);
    public decimal CollectedTotal => PaidInvoices.Sum(item => item.TotalAmount);
}

public class AddInvoiceServiceViewModel
{
    [Required]
    public int ServiceId { get; set; }
}

public class ApplyDiscountViewModel
{
    public int InvoiceId { get; set; }
    [StringLength(32)]
    public string? Code { get; set; }
}

public class DiscountCouponViewModel
{
    [Required, StringLength(32)]
    public string Code { get; set; } = string.Empty;
    [Required]
    public string DiscountType { get; set; } = "Percent";
    [Range(typeof(decimal), "0.01", "100000000")]
    public decimal Value { get; set; }
    public decimal DiscountValue { get => Value; set => Value = value; }
    [DataType(DataType.Date)]
    public DateTime StartsOn { get; set; }
    public DateTime StartDate { get => StartsOn; set => StartsOn = value; }
    [DataType(DataType.Date)]
    public DateTime EndsOn { get; set; }
    public DateTime EndDate { get => EndsOn; set => EndsOn = value; }
}

public class PaymentViewModel
{
    [Required]
    public string PaymentMethod { get; set; } = "Cash";

    [StringLength(500)]
    public string? ReconciliationNote { get; set; }
}

public class InvoiceAdjustmentViewModel
{
    [Required, StringLength(150)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "-100000000", "100000000")]
    public decimal AmountDelta { get; set; }

    [Required]
    public int InvoiceLineId { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class AppointmentDetailsViewModel
{
    public Appointment Appointment { get; set; } = null!;
    public IReadOnlyList<AppointmentHistoryRow> History { get; set; } = [];
    public bool IsNewCustomer { get; set; }
    public bool HistoryLoadFailed { get; set; }
}

public class AppointmentHistoryRow
{
    public DateTime AppointmentDate { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public string? TechnicalNotes { get; set; }
}
