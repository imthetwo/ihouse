
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.Models
{
    public class Invoice : IValidatableObject
    {
        public int Id { get; set; }

        // Contract relationship
        [Range(1, int.MaxValue,
            ErrorMessage = "Please select a valid contract.")]
        public int ContractId { get; set; }

        public Contract Contract { get; set; } = null!;

        // Invoice information
        [DataType(DataType.Date)]
        public DateOnly InvoiceDate { get; set; }

        [DataType(DataType.Date)]
        public DateOnly DueDate { get; set; }

        [Range(typeof(decimal), "0", "100000000",
            ErrorMessage = "Total amount must be between $0 and $100,000,000.")]
        public decimal TotalAmount { get; set; }

        [EnumDataType(typeof(InvoiceStatus),
            ErrorMessage = "Invalid invoice status.")]
        public InvoiceStatus Status { get; set; }

        // Relationships
        public ICollection<InvoiceItem> InvoiceItems { get; set; }
            = new List<InvoiceItem>();

        public ICollection<Payment> Payments { get; set; }
            = new List<Payment>();

        // Business validation
        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (InvoiceDate == default)
            {
                yield return new ValidationResult(
                    "Invoice date is required.",
                    new[] { nameof(InvoiceDate) });
            }

            if (DueDate == default)
            {
                yield return new ValidationResult(
                    "Due date is required.",
                    new[] { nameof(DueDate) });
            }

            if (InvoiceDate != default &&
                DueDate != default &&
                DueDate < InvoiceDate)
            {
                yield return new ValidationResult(
                    "Due date must not precede invoice date.",
                    new[] { nameof(DueDate) });
            }
        }
    }
}
