
using System.ComponentModel.DataAnnotations;

namespace ihouse.ViewModels.MeterReading;

public class MeterReadingFormViewModel
{
    [Range(1, int.MaxValue,
        ErrorMessage = "Please select a room.")]
    public int RoomId { get; set; }

    [DataType(DataType.Date)]
    public DateOnly ReadingDay { get; set; }
        = DateOnly.FromDateTime(DateTime.Today);

    [Range(typeof(decimal), "0", "999999999",
        ErrorMessage = "Electricity reading cannot be negative.")]
    public decimal ElectricityReading { get; set; }

    [Range(typeof(decimal), "0", "999999999",
        ErrorMessage = "Water reading cannot be negative.")]
    public decimal WaterReading { get; set; }
}
