
using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.ViewModels.Payment;

public class PaymentFormViewModel
{
    [Range(1, int.MaxValue,
        ErrorMessage = "Please select an invoice.")]
    public int InvoiceId { get; set; }

    [Range(typeof(decimal), "0.01", "100000000",
        ErrorMessage = "Payment amount must be greater than $0.")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; set; }

    [StringLength(100)]
    public string? TransactionCode { get; set; }
}
