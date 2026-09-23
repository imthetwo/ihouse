namespace ihouse.Models
{
    public class Room
    {
        public int Id { get; set; } //0

        public string RoomNumber { get; set; } = string.Empty; // ""

        public int Floor { get; set; } //0

        public string RoomType { get; set; } = string.Empty; // ""

        public int Capacity { get; set; } //0

        public decimal RentPrice { get; set; } //0

        public string Status { get; set; } = "Active";  //Active

        public string? Description { get; set; } // nullable        

        public string? ImageUrl { get; set; } // nullable
    }
}