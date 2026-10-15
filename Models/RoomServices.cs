namespace ihouse.Models
{
    public class RoomService
    {
        public int Id { get; set; }

        // Room relationship
        public int RoomId { get; set; }

        public Room Room { get; set; } = null!;

        // ServicePrice relationship
        public int ServicePriceId { get; set; }

        public ServicePrice ServicePrice { get; set; } = null!;

        // RoomService information
        public bool IsActive { get; set; }
    }
}