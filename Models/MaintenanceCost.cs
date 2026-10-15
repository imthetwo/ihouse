namespace ihouse.Models
{
    public class MaintenanceCost
    {
        public int Id { get; set; }

        // Foreign Key
        public int MaintenanceRequestId { get; set; }

        // Navigation Property
        public MaintenanceRequest MaintenanceRequest { get; set; } = null!;

        public decimal Amount { get; set; }

        public string? Description { get; set; }

        public bool ChargeToTenant { get; set; }
    }
}