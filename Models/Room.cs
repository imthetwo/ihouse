using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.Models
{
    public class Room
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string RoomNumber { get; set; } = string.Empty;

        [Range(1, 150)]
        public int Floor { get; set; }

        [Required]
        [StringLength(50)]
        public string RoomType { get; set; } = string.Empty;

        [Range(1, 5)]
        public int Capacity { get; set; }

        [Range(0, 1000000000)]
        public decimal RentPrice { get; set; }

        public RoomStatus Status { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Url]
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        // Relationships
        public ICollection<Contract> Contracts { get; set; }
            = new List<Contract>();

        public ICollection<RoomService> RoomServices { get; set; }
            = new List<RoomService>();

        public ICollection<MeterReading> MeterReadings { get; set; }
            = new List<MeterReading>();

        public ICollection<MaintenanceRequest> MaintenanceRequests { get; set; }
            = new List<MaintenanceRequest>();
    }
}