namespace ihouse.Models
{
    public class Contract
    {
        public int Id { get; set; }

        public int RoomId { get; set; }

        public DateOnly ContractStartDate { get; set; }

        public DateOnly ContractEndDate { get; set; }

        public DateOnly? CheckInDate { get; set; }

        public DateOnly? CheckOutDate { get; set; }

        public decimal RentPrice { get; set; }

        public decimal DepositAmount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public ContractStatus Status { get; set; }
    }
}