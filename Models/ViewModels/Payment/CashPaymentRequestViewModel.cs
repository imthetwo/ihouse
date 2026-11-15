
namespace ihouse.ViewModels.Payment;

public class CashPaymentRequestViewModel
{
    public int RequestId { get; set; }

    public int InvoiceId { get; set; }

    public int ContractId { get; set; }

    public decimal Amount { get; set; }

    public DateTime RequestedAt { get; set; }
}
