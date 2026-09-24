namespace ihouse.Models
{
    public class ContractTenant
    {
        public int ContractId { get; set; }

        public int TenantId { get; set; }

        public bool IsPrimaryTenant { get; set; }
    }
}