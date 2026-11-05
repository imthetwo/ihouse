
using System.ComponentModel.DataAnnotations;

namespace ihouse.ViewModels.Invoice;

public class InvoiceFormViewModel : IValidatableObject
{
    [Range(1, int.MaxValue,
        ErrorMessage = "Please select a contract.")]
    public int ContractId { get; set; }

    [DataType(DataType.Date)]
    public DateOnly InvoiceDate { get; set; }
        = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date)]
    public DateOnly DueDate { get; set; }
        = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (InvoiceDate == default)
        {
            yield return new ValidationResult(
                "Invoice date is required.",
                new[] { nameof(InvoiceDate) });
        }

        if (DueDate == default || DueDate < InvoiceDate)
        {
            yield return new ValidationResult(
                "Due date must not precede invoice date.",
                new[] { nameof(DueDate) });
        }
    }
}
