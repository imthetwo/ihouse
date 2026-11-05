
namespace ihouse.ViewModels.Admin;

public class DashboardViewModel
{
    public int TotalRooms { get; set; }
    public int OccupiedRooms { get; set; }
    public int AvailableRooms { get; set; }
    public int MaintenanceRooms { get; set; }

    public decimal MonthlyRevenue { get; set; }

    public int UnpaidInvoices { get; set; }
    public int OverdueInvoices { get; set; }
}
