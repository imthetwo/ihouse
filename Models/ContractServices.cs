using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.Models
{
    public class ContractService
    {
        public int Id { get; set; }

        // Contract relationship
        [Range(1, int.MaxValue,
            ErrorMessage = "Please select a valid contract.")]
        public int ContractId { get; set; }

        public Contract Contract { get; set; } = null!;

        // Service information
        [EnumDataType(typeof(ServiceType),
            ErrorMessage = "Invalid service type.")]
        public ServiceType ServiceType { get; set; }

        [Range(typeof(decimal), "0", "400",
            ErrorMessage = "Unit price must be between $0 and $400.")]
        public decimal UnitPrice { get; set; }

        public bool IsActive { get; set; }
    }
}