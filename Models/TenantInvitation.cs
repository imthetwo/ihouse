
using System;
using System.ComponentModel.DataAnnotations;

namespace ihouse.Models
{
	public class TenantInvitation
	{
		public int Id { get; set; }

		// Tenant profile that will be linked
		[Range(1, int.MaxValue)]
		public int TenantId { get; set; }

		// Contract assigned to this tenant
		[Range(1, int.MaxValue)]
		public int ContractId { get; set; }

		// SHA-256 hash of invitation code
		[Required]
		[MaxLength(64)]
		public string CodeHash { get; set; } = string.Empty;

		// Invitation lifetime
		public DateTime CreatedAt { get; set; }

		public DateTime ExpiresAt { get; set; }

		// One-time redemption
		public DateTime? RedeemedAt { get; set; }

		public string? RedeemedByUserId { get; set; }
	}
}
