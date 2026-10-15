using ihouse.Models.Enums;

namespace ihouse.Models
{
    public class Payment
    {
        public int Id { get; set; }

        // Invoice relationship
        public int InvoiceId { get; set; }

        public Invoice Invoice { get; set; } = null!;

        // Payment information
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public string? TransactionCode { get; set; }
    }
}