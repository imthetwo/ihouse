
using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.ViewModels.ServicePrice;

public class ServicePriceFormViewModel : IValidatableObject
{
    [EnumDataType(typeof(ServiceType),
        ErrorMessage = "Invalid service type.")]
    public ServiceType ServiceType { get; set; }

    [Range(typeof(decimal), "0", "100000000",
        ErrorMessage = "Price must be between $0 and $100,000,000.")]
    public decimal Price { get; set; }

    [DataType(DataType.Date)]
    public DateOnly EffectiveFrom { get; set; }
        = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date)]
    public DateOnly? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (EffectiveFrom == default)
        {
            yield return new ValidationResult(
                "Effective From is required.",
                new[] { nameof(EffectiveFrom) });
        }

        if (EffectiveTo.HasValue &&
            EffectiveTo.Value < EffectiveFrom)
        {
            yield return new ValidationResult(
                "Effective To cannot be earlier than Effective From.",
                new[] { nameof(EffectiveTo) });
        }
    }
}
