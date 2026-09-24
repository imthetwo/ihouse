namespace ihouse.Models
{
    public class Invoice
    {
        public int Id { get; set; }

        public int ContractId { get; set; }

        public DateOnly InvoiceDate { get; set; }

        public DateOnly DueDate { get; set; }

        public decimal TotalAmount { get; set; }

        public InvoiceStatus Status { get; set; }
    }
}