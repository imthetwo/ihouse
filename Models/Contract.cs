using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.Models
{
    public class Contract
    {
        public int Id { get; set; }

        // Room relationship

        [Range(1, int.MaxValue,
            ErrorMessage = "Please select a valid room.")]
        public int RoomId { get; set; }

        public Room Room { get; set; } = null!;

        // Contract information

        public DateOnly ContractStartDate { get; set; }

        public DateOnly ContractEndDate { get; set; }

        public DateOnly? CheckInDate { get; set; }

        public DateOnly? CheckOutDate { get; set; }

        [Range(typeof(decimal), "1", "2000",
            ErrorMessage = "Monthly rent must be between $1 and $2,000.")]
        public decimal RentPrice { get; set; }

        [Range(typeof(decimal), "0", "4000",
            ErrorMessage = "Deposit must be between $0 and $4,000.")]
        public decimal DepositAmount { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public ContractStatus Status { get; set; }

        // Relationships

        public ICollection<ContractTenant> ContractTenants { get; set; }
            = new List<ContractTenant>();

        public ICollection<ContractService> ContractServices { get; set; }
            = new List<ContractService>();

        public ICollection<Invoice> Invoices { get; set; }
            = new List<Invoice>();
    }
}