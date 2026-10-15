namespace ihouse.Models
{
    public class MeterReading
    {
        public int Id { get; set; }

        // Room relationship
        public int RoomId { get; set; }

        public Room Room { get; set; } = null!;

        // Meter reading information
        public DateOnly ReadingDay { get; set; }

        public decimal ElectricityReading { get; set; }

        public decimal WaterReading { get; set; }
    }
}