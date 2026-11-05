
using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.ViewModels.Room;

public class RoomFormViewModel
{
    [Required(ErrorMessage = "Room number is required.")]
    [StringLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    [Range(1, 150)]
    public int Floor { get; set; }

    [Required(ErrorMessage = "Room type is required.")]
    [StringLength(50)]
    public string RoomType { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Capacity { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal RentPrice { get; set; }

    [EnumDataType(typeof(RoomStatus))]
    public RoomStatus Status { get; set; } = RoomStatus.Active;

    [StringLength(500)]
    public string? Description { get; set; }

    [Url]
    [StringLength(500)]
    public string? ImageUrl { get; set; }
}
