
using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.ViewModels.Maintenance;

public class MaintenanceFormViewModel
{
    [Range(1, int.MaxValue,
        ErrorMessage = "Please select a room.")]
    public int RoomId { get; set; }

    [Range(1, int.MaxValue,
        ErrorMessage = "Please select a tenant.")]
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(MaintenanceRqStatus))]
    public MaintenanceRqStatus Status { get; set; }
        = MaintenanceRqStatus.Pending;

    public DateTime? CompletedAt { get; set; }
}
