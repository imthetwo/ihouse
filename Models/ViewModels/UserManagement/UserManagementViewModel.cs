
using System.ComponentModel.DataAnnotations;

namespace ihouse.ViewModels.UserManagement;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }
}

public class EditUserRoleViewModel
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string CurrentRole { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a role.")]
    public string NewRole { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your admin password.")]
    [DataType(DataType.Password)]
    public string AdminPassword { get; set; } = string.Empty;
}
