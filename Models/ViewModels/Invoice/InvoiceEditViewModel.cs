
using System.ComponentModel.DataAnnotations;

namespace ihouse.ViewModels.Invoice;

public class InvoiceEditViewModel
{
    public int Id { get; set; }

    public int ContractId { get; set; }

    [Required]
    [Display(Name = "Invoice Date")]
    public DateOnly InvoiceDate { get; set; }

    [Required]
    [Display(Name = "Due Date")]
    public DateOnly DueDate { get; set; }

    [Required]
    [Range(
        typeof(decimal),
        "0.01",
        "999999999999.99",
        ErrorMessage = "Rent amount must be greater than zero.")]
    [Display(Name = "Monthly Rent (USD)")]
    public decimal RentAmount { get; set; }
}
