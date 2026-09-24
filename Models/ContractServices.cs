namespace ihouse.Models
{
    public class ContractService
    {
        public int Id { get; set; }

        public int ContractId { get; set; }

        public ServiceType ServiceType { get; set; }

        public decimal UnitPrice { get; set; }

        public bool IsActive { get; set; }
    }
}