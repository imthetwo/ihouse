namespace ihouse.Models
{
    public class RoomService
    {
        public int Id { get; set; }

        public int RoomId { get; set; }

        public int ServicePriceId { get; set; }

        public bool IsActive { get; set; }
    }
}