using System.Data;
using ihouse.Data;
using ihouse.Models;
using ihouse.Models.Enums;
using ihouse.ViewModels.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        ApplicationDbContext context,
        ILogger<PaymentController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // PAYMENT LIST
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var payments = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Invoice)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        ViewBag.PendingCashRequests =
            await _context.PaymentTransactions
                .AsNoTracking()
                .Where(t =>
                    t.PaymentMethod == PaymentMethod.Cash &&
                    t.Status == PaymentTransactionStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Select(t => new CashPaymentRequestViewModel
                {
                    RequestId = t.Id,
                    InvoiceId = t.InvoiceId,
                    ContractId = t.Invoice.ContractId,
                    Amount = t.AmountUSD,
                    RequestedAt = t.CreatedAt
                })
                .ToListAsync();

        return View(payments);
    }

    // CREATE CASH PAYMENT - GET
    [HttpGet]
    public async Task<IActionResult> Create(int? invoiceId)
    {
        await LoadInvoicesAsync();

        var model = new PaymentFormViewModel
        {
            PaymentMethod = PaymentMethod.Cash,

            PaymentDate = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                GetPaymentTimeZone())
        };

        if (invoiceId.HasValue)
        {
            model.InvoiceId = invoiceId.Value;
        }

        return View(model);
    }

    // CREATE CASH PAYMENT - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PaymentFormViewModel model)
    {
        // Only Cash can be manually recorded.
        // PayPal is handled by PayPalController.
        if (model.PaymentMethod != PaymentMethod.Cash)
        {
            ModelState.AddModelError(
                nameof(model.PaymentMethod),
                "Only cash payments can be recorded here.");
        }

        // Cash receipt numbers are generated automatically.
        // Ignore any reference submitted by the browser.
        model.TransactionCode = null;

        ModelState.Remove(nameof(model.TransactionCode));

        DateTime paymentDateUtc = default;

        if (model.PaymentDate == default)
        {
            ModelState.AddModelError(
                nameof(model.PaymentDate),
                "Payment date is required.");
        }
        else
        {
            try
            {
                var localDate = DateTime.SpecifyKind(
                    model.PaymentDate,
                    DateTimeKind.Unspecified);

                paymentDateUtc =
                    TimeZoneInfo.ConvertTimeToUtc(
                        localDate,
                        GetPaymentTimeZone());

                if (paymentDateUtc > DateTime.UtcNow.AddMinutes(5))
                {
                    ModelState.AddModelError(
                        nameof(model.PaymentDate),
                        "Payment date cannot be in the future.");
                }
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(
                    nameof(model.PaymentDate),
                    "Invalid payment date.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadInvoicesAsync();
            return View(model);
        }

        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i =>
                    i.Id == model.InvoiceId);

            if (invoice == null)
            {
                ModelState.AddModelError(
                    nameof(model.InvoiceId),
                    "Invoice not found.");

                await transaction.RollbackAsync();
                await LoadInvoicesAsync();

                return View(model);
            }

            bool hasPendingTransaction =
                await _context.PaymentTransactions
                    .AnyAsync(t =>
                        t.InvoiceId == invoice.Id &&
                        t.Status == PaymentTransactionStatus.Pending);

            if (hasPendingTransaction)
            {
                ModelState.AddModelError(
                    nameof(model.InvoiceId),
                    "This invoice has a payment in progress. " +
                    "Please resolve it before recording another payment.");

                await transaction.RollbackAsync();
                await LoadInvoicesAsync();

                return View(model);
            }

            decimal paid =
                invoice.Payments.Sum(p => p.Amount);

            decimal remaining =
                invoice.TotalAmount - paid;

            if (remaining <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.InvoiceId),
                    "This invoice has already been paid.");

                await transaction.RollbackAsync();
                await LoadInvoicesAsync();

                return View(model);
            }

            if (model.Amount <= 0 ||
                model.Amount > remaining)
            {
                ModelState.AddModelError(
                    nameof(model.Amount),
                    $"Enter an amount between $0.01 and ${remaining:N2}.");

                await transaction.RollbackAsync();
                await LoadInvoicesAsync();

                return View(model);
            }

            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Amount = model.Amount,
                PaymentDate = paymentDateUtc,
                PaymentMethod = PaymentMethod.Cash,
                TransactionCode = null
            };

            _context.Payments.Add(payment);

            decimal newPaid =
                paid + model.Amount;

            invoice.Status =
                newPaid >= invoice.TotalAmount
                    ? InvoiceStatus.Paid
                    : InvoiceStatus.PartiallyPaid;

            // Save first to generate Payment.Id.
            await _context.SaveChangesAsync();

            payment.TransactionCode =
                $"CASH-{payment.Id:D6}";

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["Success"] =
                $"Cash payment #{payment.Id} recorded successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id = payment.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to create cash payment for invoice {InvoiceId}",
                model.InvoiceId);

            ModelState.AddModelError(
                "",
                "Unable to save the payment. " +
                "Please try again.");

            await LoadInvoicesAsync();

            return View(model);
        }
    }

    // APPROVE TENANT CASH REQUEST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCash(int requestId)
    {
        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var request = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t =>
                    t.Id == requestId &&
                    t.PaymentMethod == PaymentMethod.Cash);

            if (request == null)
            {
                TempData["Error"] =
                    "Cash payment request not found.";

                return RedirectToAction(nameof(Index));
            }

            if (request.Status != PaymentTransactionStatus.Pending)
            {
                TempData["Error"] =
                    "This payment request has already been processed.";

                return RedirectToAction(nameof(Index));
            }

            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i =>
                    i.Id == request.InvoiceId);

            if (invoice == null)
            {
                TempData["Error"] =
                    "Invoice not found. Payment was not recorded.";

                return RedirectToAction(nameof(Index));
            }

            decimal alreadyPaid =
                invoice.Payments.Sum(p => p.Amount);

            decimal remaining =
                invoice.TotalAmount - alreadyPaid;

            if (request.AmountUSD <= 0 ||
                remaining != request.AmountUSD)
            {
                TempData["Error"] =
                    "The invoice balance has changed. " +
                    "Please review this request.";

                return RedirectToAction(nameof(Index));
            }

            // Staff must only approve after receiving the cash.
            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Amount = request.AmountUSD,
                PaymentDate = DateTime.UtcNow,
                PaymentMethod = PaymentMethod.Cash,
                TransactionCode = null
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            payment.TransactionCode =
                $"CASH-{payment.Id:D6}";

            request.Status =
                PaymentTransactionStatus.Success;

            request.CompletedAt =
                DateTime.UtcNow;

            decimal newPaid =
                alreadyPaid + payment.Amount;

            invoice.Status =
                newPaid >= invoice.TotalAmount
                    ? InvoiceStatus.Paid
                    : InvoiceStatus.PartiallyPaid;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["Success"] =
                $"Cash payment #{payment.Id} confirmed successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to approve cash request {RequestId}",
                requestId);

            TempData["Error"] =
                "Unable to confirm this payment. " +
                "Please check its current status.";

            return RedirectToAction(nameof(Index));
        }
    }

    // REJECT TENANT CASH REQUEST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCash(int requestId)
    {
        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var request = await _context.PaymentTransactions
                .FirstOrDefaultAsync(t =>
                    t.Id == requestId &&
                    t.PaymentMethod == PaymentMethod.Cash);

            if (request == null)
            {
                TempData["Error"] =
                    "Cash payment request not found.";

                return RedirectToAction(nameof(Index));
            }

            if (request.Status != PaymentTransactionStatus.Pending)
            {
                TempData["Error"] =
                    "This request has already been processed.";

                return RedirectToAction(nameof(Index));
            }

            // Reject the request without creating a Payment.
            request.Status =
                PaymentTransactionStatus.Failed;

            request.CompletedAt =
                DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["Success"] =
                $"Cash request #{requestId} declined. " +
                "The invoice remains payable.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to reject cash request {RequestId}",
                requestId);

            TempData["Error"] =
                "Unable to decline this payment request.";

            return RedirectToAction(nameof(Index));
        }
    }

    // PAYMENT DETAILS
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.Payments
            .AsNoTracking()
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
        {
            return NotFound();
        }

        return View(payment);
    }

    // LOAD AVAILABLE INVOICES
    private async Task LoadInvoicesAsync()
    {
        ViewBag.InvoiceOptions =
            await _context.Invoices
                .AsNoTracking()
                .Where(i =>
                    i.TotalAmount >
                    (i.Payments.Sum(p => (decimal?)p.Amount)
                        ?? 0m))
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.Id)
                .Select(i => new SelectListItem
                {
                    Value = i.Id.ToString(),

                    Text = "Invoice #" + i.Id +
                           " - Contract #" + i.ContractId
                })
                .ToListAsync();
    }

    // VIETNAM TIME ZONE
    private static TimeZoneInfo GetPaymentTimeZone()
    {
        return TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows()
                ? "SE Asia Standard Time"
                : "Asia/Ho_Chi_Minh");
    }
}