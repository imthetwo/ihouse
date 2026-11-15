using System.ComponentModel.DataAnnotations;

namespace ihouse.ViewModels.Maintenance;

public class MaintenanceCostFormViewModel : IValidatableObject
{
    [Required, StringLength(500)]
    public string Description { get; set; } = "";

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Amount { get; set; }

    public bool ChargeToTenant { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (decimal.Round(Amount, 2) != Amount)
            yield return new ValidationResult("Use at most two decimal places.", new[] { nameof(Amount) });
    }
}
