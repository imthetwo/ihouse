
using System;
using System.Collections.Generic;

namespace ihouse.ViewModels
{
	public class TenantDashboardViewModel
	{
		// Account
		public bool TenantLinked { get; set; }
		public string TenantName { get; set; } = string.Empty;

		// Room & Contract
		public RoomInfo? Room { get; set; }
		public ContractInfo? Contract { get; set; }

		// Invoices & Payments
		public List<InvoiceInfo> Invoices { get; set; } = new();
		public List<PaymentInfo> Payments { get; set; } = new();

		public decimal OutstandingBalance { get; set; }
		public DateOnly? NextDueDate { get; set; }

		public int UnpaidInvoiceCount =>
			Invoices.FindAll(i => i.Balance > 0).Count;

		// Maintenance
		public List<MaintenanceInfo> MaintenanceRequests { get; set; }
			= new();
	}

	public class RoomInfo
	{
		public int Id { get; set; }
		public string RoomNumber { get; set; } = string.Empty;
		public int Floor { get; set; }
		public string RoomType { get; set; } = string.Empty;
		public int Capacity { get; set; }
		public string? Description { get; set; }
		public string? ImageUrl { get; set; }
	}

	public class ContractInfo
	{
		public int Id { get; set; }
		public int RoomId { get; set; }

		public DateOnly StartDate { get; set; }
		public DateOnly EndDate { get; set; }

		public decimal MonthlyRent { get; set; }
		public decimal DepositAmount { get; set; }

		public string Status { get; set; } = string.Empty;
		public string PaymentMethod { get; set; } = string.Empty;
	}

	public class InvoiceInfo
	{
		public int Id { get; set; }
		public DateOnly InvoiceDate { get; set; }
		public DateOnly DueDate { get; set; }

		public decimal TotalAmount { get; set; }
		public decimal PaidAmount { get; set; }

		public decimal Balance =>
			Math.Max(0, TotalAmount - PaidAmount);

		public string Status { get; set; } = string.Empty;

		public List<InvoiceItemInfo> Items { get; set; } = new();
	}

	public class InvoiceItemInfo
	{
		public string ItemType { get; set; } = string.Empty;
		public string? Description { get; set; }

		public decimal Quantity { get; set; }
		public decimal UnitPrice { get; set; }
		public decimal Amount { get; set; }
	}

	public class PaymentInfo
	{
		public int Id { get; set; }
		public int InvoiceId { get; set; }

		public decimal Amount { get; set; }
		public DateTime PaymentDate { get; set; }

		public string PaymentMethod { get; set; } = string.Empty;
		public string? TransactionCode { get; set; }
	}

	public class MaintenanceInfo
	{
		public int Id { get; set; }
		public int RoomId { get; set; }

		public string Description { get; set; } = string.Empty;

		public string Status { get; set; } = string.Empty;
		public string ApprovalStatus { get; set; } = string.Empty;

		public DateTime CreatedAt { get; set; }
		public DateTime? CompletedAt { get; set; }

		public decimal ProposedTenantCost { get; set; }

		public List<MaintenanceCostInfo> Costs { get; set; } = new();
	}

	public class MaintenanceCostInfo
	{
		public int Id { get; set; }
		public string? Description { get; set; }
		public decimal Amount { get; set; }
		public bool ChargeToTenant { get; set; }
	}
}
