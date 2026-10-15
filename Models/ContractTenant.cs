using System.ComponentModel.DataAnnotations;

namespace ihouse.Models
{
    public class ContractTenant
    {
        [Range(1, int.MaxValue,
            ErrorMessage = "Please select a valid contract.")]
        public int ContractId { get; set; }

        [Range(1, int.MaxValue,
            ErrorMessage = "Please select a valid tenant.")]
        public int TenantId { get; set; }

        public bool IsPrimaryTenant { get; set; }

        public Contract Contract { get; set; } = null!;

        public Tenant Tenant { get; set; } = null!;
    }
}