namespace ihouse.Models
{
    public class MeterReading
    {
        public int Id { get; set; };

        public int RoomId { get; set; };

        public DateOnly ReadingDay { get; set; };

        public decimal ElectricityReading { get; set; };

        public decimal WaterReading { get; set; };
    }
}
