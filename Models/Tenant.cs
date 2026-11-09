
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Models
{
    [Index(nameof(ApplicationUserId), IsUnique = true)]
    public class Tenant
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(12)]
        public string IdentityNumber { get; set; } = string.Empty;

        [Phone]
        public string? Phone { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        // Link to ASP.NET Core Identity account
        public string? ApplicationUserId { get; set; }

        public ApplicationUser? ApplicationUser { get; set; }

        // ContractTenant relationship
        public ICollection<ContractTenant> ContractTenants { get; set; }
            = new List<ContractTenant>();

        // MaintenanceRequest relationship
        public ICollection<MaintenanceRequest> MaintenanceRequests { get; set; }
            = new List<MaintenanceRequest>();
    }
}
