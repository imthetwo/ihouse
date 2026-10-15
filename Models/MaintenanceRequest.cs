using ihouse.Models.Enums;

namespace ihouse.Models
{
    public class MaintenanceRequest
    {
        public int Id { get; set; }

        // Room relationship
        public int RoomId { get; set; }

        public Room Room { get; set; } = null!;

        // Tenant relationship
        public int TenantId { get; set; }

        public Tenant Tenant { get; set; } = null!;

        // Maintenance information
        public string Description { get; set; } = string.Empty;

        public MaintenanceRqStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        // MaintenanceCost relationship
        public ICollection<MaintenanceCost> MaintenanceCosts { get; set; }
            = new List<MaintenanceCost>();
    }
}