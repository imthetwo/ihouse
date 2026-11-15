
using ihouse.Data;
using ihouse.Models.Enums;
using ihouse.ViewModels.Payment;
using ihouse.ViewModels.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Staff")]
public class StaffController : Controller
{
    private readonly ApplicationDbContext _context;

    public StaffController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new StaffDashboardViewModel
        {
            TotalRooms = await _context.Rooms
                .AsNoTracking()
                .CountAsync(),

            TotalTenants = await _context.Tenants
                .AsNoTracking()
                .CountAsync(),

            UnpaidInvoices = await _context.Invoices
                .AsNoTracking()
                .CountAsync(i =>
                    i.TotalAmount >
                    (i.Payments.Sum(p => (decimal?)p.Amount)
                        ?? 0m)),

            PendingCashPayments =
                await _context.PaymentTransactions
                    .AsNoTracking()
                    .CountAsync(t =>
                        t.PaymentMethod == PaymentMethod.Cash &&
                        t.Status ==
                            PaymentTransactionStatus.Pending),

            RecentCashRequests =
                await _context.PaymentTransactions
                    .AsNoTracking()
                    .Where(t =>
                        t.PaymentMethod == PaymentMethod.Cash &&
                        t.Status ==
                            PaymentTransactionStatus.Pending)
                    .OrderByDescending(t => t.CreatedAt)
                    .ThenByDescending(t => t.Id)
                    .Take(5)
                    .Select(t => new CashPaymentRequestViewModel
                    {
                        RequestId = t.Id,
                        InvoiceId = t.InvoiceId,
                        ContractId = t.Invoice.ContractId,
                        Amount = t.AmountUSD,
                        RequestedAt = t.CreatedAt
                    })
                    .ToListAsync()
        };

        return View(model);
    }
}
