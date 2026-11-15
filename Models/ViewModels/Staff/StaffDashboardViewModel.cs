
using ihouse.ViewModels.Payment;

namespace ihouse.ViewModels.Staff;

public class StaffDashboardViewModel
{
    public int TotalRooms { get; set; }

    public int TotalTenants { get; set; }

    public int UnpaidInvoices { get; set; }

    public int PendingCashPayments { get; set; }

    public List<CashPaymentRequestViewModel> RecentCashRequests
    { get; set; } = new();
}
