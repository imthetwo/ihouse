using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.Models
{
    public class InvoiceItem
    {
        public int Id { get; set; }

        // Invoice relationship
        [Range(1, int.MaxValue,
            ErrorMessage = "Please select a valid invoice.")]
        public int InvoiceId { get; set; }

        public Invoice Invoice { get; set; } = null!;

        // Invoice item information
        [EnumDataType(typeof(InvoiceItemType),
            ErrorMessage = "Invalid invoice item type.")]
        public InvoiceItemType ItemType { get; set; }

        [StringLength(500,
            ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Range(typeof(decimal), "0.01", "1000000",
            ErrorMessage = "Quantity must be greater than 0 and not exceed 1,000,000.")]
        public decimal Quantity { get; set; }

        [Range(typeof(decimal), "0", "100000000",
            ErrorMessage = "Unit price must be between 0 and 100,000,000 VND.")]
        public decimal UnitPrice { get; set; }

        [Range(typeof(decimal), "0", "100000000",
            ErrorMessage = "Amount must be between 0 and 100,000,000 VND.")]
        public decimal Amount { get; set; }
    }
}