namespace ihouse.Models
{
    public class MaintenanceCost
    {
        public int Id { get; set; }

        public int MaintenanceRequestId { get; set; }

        public decimal Amount { get; set; }

        public string? Description { get; set; }    

        public bool ChargeToTenant { get; set; }
    }
}