
using System.ComponentModel.DataAnnotations;

namespace ihouse.ViewModels.Tenant;

public class TenantFormViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Identity number is required.")]
    [StringLength(12)]
    public string IdentityNumber { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Invalid phone number.")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email address.")]
    public string? Email { get; set; }
}
