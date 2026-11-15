using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.ViewModels.Payment;

public class PaymentFormViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Please select an invoice.")]
    [Range(
        1,
        int.MaxValue,
        ErrorMessage = "Please select a valid invoice.")]
    [Display(Name = "Invoice")]
    public int InvoiceId { get; set; }

    [Required]
    [Range(
        typeof(decimal),
        "0.01",
        "999999999999.99",
        ErrorMessage = "Amount must be greater than zero.")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Required]
    [EnumDataType(
        typeof(PaymentMethod),
        ErrorMessage = "Invalid payment method.")]
    [Display(Name = "Payment Method")]
    public PaymentMethod PaymentMethod { get; set; }
        = PaymentMethod.Cash;

    [Required(ErrorMessage = "Payment date is required.")]
    [DataType(DataType.DateTime)]
    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; }

    [StringLength(
        200,
        ErrorMessage = "Transaction reference cannot exceed 200 characters.")]
    [Display(Name = "Transaction Reference")]
    public string? TransactionCode { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        // Manual payments can only be Cash.
        // PayPal payments are handled automatically.
        if (PaymentMethod != PaymentMethod.Cash)
        {
            yield return new ValidationResult(
                "Only cash payments can be recorded manually.",
                new[] { nameof(PaymentMethod) });
        }
    }
}