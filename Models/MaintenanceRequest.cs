namespace ihouse.Models
{
    public class MaintenanceRequest
    {
        public int Id { get; set; }

        public int RoomId { get; set; }

        public int TenantId { get; set; }

        public string Description { get; set; } = string.Empty;

        public MaintenanceRqStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}