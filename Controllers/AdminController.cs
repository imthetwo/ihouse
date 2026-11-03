
using ihouse.Data;
using ihouse.Models.Enums;
using ihouse.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Current date
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Current month (UTC)
        var now = DateTime.UtcNow;

        var monthStart = new DateTime(
            now.Year,
            now.Month,
            1,
            0, 0, 0,
            DateTimeKind.Utc
        );

        var nextMonth = monthStart.AddMonths(1);

        // ================= ROOMS =================

        var totalRooms = await _context.Rooms
            .AsNoTracking()
            .CountAsync();

        var maintenanceRooms = await _context.Rooms
            .AsNoTracking()
            .CountAsync(r =>
                r.Status == RoomStatus.Maintenance
            );

        // Active contracts within the current date
        var occupiedRooms = await _context.Rooms
            .AsNoTracking()
            .CountAsync(r =>
                r.Status == RoomStatus.Active &&
                r.Contracts.Any(c =>
                    c.Status == ContractStatus.Active &&
                    c.ContractStartDate <= today &&
                    c.ContractEndDate >= today
                )
            );

        var availableRooms = await _context.Rooms
            .AsNoTracking()
            .CountAsync(r =>
                r.Status == RoomStatus.Active &&
                !r.Contracts.Any(c =>
                    c.Status == ContractStatus.Active &&
                    c.ContractStartDate <= today &&
                    c.ContractEndDate >= today
                )
            );

        // ================= FINANCIAL =================

        var monthlyRevenue = await _context.Payments
            .AsNoTracking()
            .Where(p =>
                p.PaymentDate >= monthStart &&
                p.PaymentDate < nextMonth
            )
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var unpaidInvoices = await _context.Invoices
            .AsNoTracking()
            .CountAsync(i =>
                i.Status == InvoiceStatus.Unpaid ||
                i.Status == InvoiceStatus.PartiallyPaid
            );

        var overdueInvoices = await _context.Invoices
            .AsNoTracking()
            .CountAsync(i =>
                (
                    i.Status == InvoiceStatus.Unpaid ||
                    i.Status == InvoiceStatus.PartiallyPaid
                ) &&
                i.DueDate < today
            );

        // ================= VIEWMODEL =================

        var model = new DashboardViewModel
        {
            TotalRooms = totalRooms,
            OccupiedRooms = occupiedRooms,
            AvailableRooms = availableRooms,
            MaintenanceRooms = maintenanceRooms,

            MonthlyRevenue = monthlyRevenue,
            UnpaidInvoices = unpaidInvoices,
            OverdueInvoices = overdueInvoices
        };

        return View(model);
    }
}
