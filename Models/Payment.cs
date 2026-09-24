namespace ihouse.Models
{
    public class Payment
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }

        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public string? TransactionCode { get; set; }
    }
}