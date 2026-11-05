
using System.ComponentModel.DataAnnotations;
using ihouse.Models.Enums;

namespace ihouse.ViewModels.Contract;

public class ContractFormViewModel : IValidatableObject
{
    [Range(1, int.MaxValue,
        ErrorMessage = "Please select a room.")]
    public int RoomId { get; set; }

    [Range(1, int.MaxValue,
        ErrorMessage = "Please select a tenant.")]
    public int PrimaryTenantId { get; set; }

    [DataType(DataType.Date)]
    public DateOnly ContractStartDate { get; set; }
        = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date)]
    public DateOnly ContractEndDate { get; set; }
        = DateOnly.FromDateTime(DateTime.Today.AddYears(1));

    [DataType(DataType.Date)]
    public DateOnly? CheckInDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? CheckOutDate { get; set; }

    [Range(typeof(decimal), "1", "2000",
        ErrorMessage = "Monthly rent must be between $1 and $2,000.")]
    public decimal RentPrice { get; set; }

    [Range(typeof(decimal), "0", "4000",
        ErrorMessage = "Deposit must be between $0 and $4,000.")]
    public decimal DepositAmount { get; set; }

    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; set; }

    [EnumDataType(typeof(ContractStatus))]
    public ContractStatus Status { get; set; } = ContractStatus.Pending;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (ContractEndDate <= ContractStartDate)
        {
            yield return new ValidationResult(
                "Contract end date must be after start date.",
                new[] { nameof(ContractEndDate) });
        }

        if (CheckInDate.HasValue &&
            CheckOutDate.HasValue &&
            CheckOutDate.Value < CheckInDate.Value)
        {
            yield return new ValidationResult(
                "Check-out date cannot be earlier than check-in date.",
                new[] { nameof(CheckOutDate) });
        }
    }
}
