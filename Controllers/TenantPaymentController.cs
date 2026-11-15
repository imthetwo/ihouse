using ihouse.Data;
using ihouse.Models;
using ihouse.Models.Enums;
using ihouse.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Tenant")]
public class TenantPaymentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PayPalService _paypal;
    private readonly ILogger<TenantPaymentsController> _logger;

    public TenantPaymentsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        PayPalService paypal,
        ILogger<TenantPaymentsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _paypal = paypal;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(int id)
    {
        var invoice = await GetTenantInvoiceAsync(id);

        if (invoice == null)
            return NotFound();

        ViewBag.HasPendingCashRequest =
            await _context.PaymentTransactions
                .AsNoTracking()
                .AnyAsync(t =>
                    t.InvoiceId == id &&
                    t.PaymentMethod == PaymentMethod.Cash &&
                    t.Status == PaymentTransactionStatus.Pending);

        ViewBag.HasPendingPayPal =
            await _context.PaymentTransactions
                .AsNoTracking()
                .AnyAsync(t =>
                    t.InvoiceId == id &&
                    t.PaymentMethod == PaymentMethod.PayPal &&
                    t.Status == PaymentTransactionStatus.Pending);

        ViewBag.PendingPayPalOrderId = await _context.PaymentTransactions
            .Where(t => t.InvoiceId == id && t.PaymentMethod == PaymentMethod.PayPal &&
                t.Status == PaymentTransactionStatus.Pending &&
                t.RequestedByUserId == _userManager.GetUserId(User))
            .Select(t => t.OrderId).FirstOrDefaultAsync();
        return View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCash(int invoiceId)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Invoices);
        // Older versions could leave an unfinished local placeholder after a crash.
        await _context.PaymentTransactions
            .Where(t => t.InvoiceId == invoiceId && t.Status == PaymentTransactionStatus.Pending &&
                t.OrderId.StartsWith("INIT-") && t.CreatedAt < DateTime.UtcNow.AddMinutes(-2))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, PaymentTransactionStatus.Expired)
                .SetProperty(t => t.CompletedAt, (DateTime?)DateTime.UtcNow));
        var invoice = await GetTenantInvoiceAsync(invoiceId);

        if (invoice == null)
            return NotFound();

        decimal balance =
            invoice.TotalAmount -
            invoice.Payments.Sum(p => p.Amount);

        if (balance <= 0)
        {
            TempData["Error"] =
                "This invoice has already been paid.";

            return RedirectToCheckout(invoiceId);
        }

        bool pending = await _context.PaymentTransactions
            .AnyAsync(t =>
                t.InvoiceId == invoiceId &&
                t.Status == PaymentTransactionStatus.Pending &&
                (
                    t.PaymentMethod == PaymentMethod.Cash ||
                    t.PaymentMethod == PaymentMethod.PayPal
                ));

        if (pending)
        {
            TempData["Error"] =
                "You already have a payment in progress.";

            return RedirectToCheckout(invoiceId);
        }

        var transaction = new PaymentTransaction
        {
            InvoiceId = invoiceId,

            OrderId =
                "CASH-" + Guid.NewGuid().ToString("N"),

            PaymentMethod = PaymentMethod.Cash,

            RequestedByUserId =
                _userManager.GetUserId(User),

            AmountUSD = balance,
            AmountVND = 0,
            ExchangeRate = 0,

            Status = PaymentTransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentTransactions.Add(transaction);

        await _context.SaveChangesAsync();
        await write.CommitAsync();

        TempData["Success"] =
            "Your cash payment request has been submitted. " +
            "Please pay the staff directly.";

        return RedirectToCheckout(invoiceId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayWithPayPal(int invoiceId)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Invoices);
        // Older versions could leave an unfinished local placeholder after a crash.
        await _context.PaymentTransactions
            .Where(t => t.InvoiceId == invoiceId && t.Status == PaymentTransactionStatus.Pending &&
                t.OrderId.StartsWith("INIT-") && t.CreatedAt < DateTime.UtcNow.AddMinutes(-2))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, PaymentTransactionStatus.Expired)
                .SetProperty(t => t.CompletedAt, (DateTime?)DateTime.UtcNow));
        var invoice = await GetTenantInvoiceAsync(invoiceId);

        if (invoice == null)
            return NotFound();

        decimal balance =
            invoice.TotalAmount -
            invoice.Payments.Sum(p => p.Amount);

        if (balance <= 0)
        {
            TempData["Error"] =
                "This invoice has already been paid.";

            return RedirectToCheckout(invoiceId);
        }

        if (decimal.Round(balance, 2) != balance)
        {
            TempData["Error"] =
                "This invoice cannot be paid online right now.";

            return RedirectToCheckout(invoiceId);
        }

        string? userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var pending = await _context.PaymentTransactions
            .Where(t =>
                t.InvoiceId == invoiceId &&
                t.Status == PaymentTransactionStatus.Pending)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync();

        if (pending != null)
        {
            if (pending.PaymentMethod != PaymentMethod.PayPal ||
                pending.RequestedByUserId != userId)
            {
                TempData["Error"] =
                    "This invoice already has a payment in progress.";

                return RedirectToCheckout(invoiceId);
            }

            if (pending.OrderId.StartsWith("INIT-"))
            {
                TempData["Error"] =
                    "Checkout is not ready yet. Please try again in a moment.";

                return RedirectToCheckout(invoiceId);
            }

            try
            {
                var order = await _paypal.GetOrderAsync(
                    pending.OrderId);

                // Verify this PayPal order belongs to the invoice.
                if (order.OrderId != pending.OrderId ||
                    order.ReferenceId != $"IHOUSE-{invoiceId}" ||
                    order.Currency != "USD" ||
                    order.OrderAmount != pending.AmountUSD)
                {
                    TempData["Error"] =
                        "We couldn't verify your payment. " +
                        "Please contact your property manager.";

                    return RedirectToCheckout(invoiceId);
                }

                // Explicitly failed: release this pending request.
                if (order.IsFailed)
                {
                    pending.Status =
                        PaymentTransactionStatus.Failed;

                    await _context.SaveChangesAsync();
                    await write.CommitAsync();

                    TempData["Error"] =
                        "Your previous payment was unsuccessful. " +
                        "You can try again.";

                    return RedirectToCheckout(invoiceId);
                }

                // Already approved or captured:
                // return to our confirmation flow.
                if (order.IsCaptured ||
                    order.Status == "APPROVED" ||
                    order.Status == "COMPLETED")
                {
                    return RedirectToAction(
                        "Return",
                        "PayPal",
                        new { token = pending.OrderId });
                }

                // Awaiting customer approval.
                if (order.Status is
                    "CREATED" or "PAYER_ACTION_REQUIRED")
                {
                    if (!string.IsNullOrWhiteSpace(order.ApprovalUrl))
                    {
                        return Redirect(order.ApprovalUrl);
                    }
                }

                TempData["Error"] =
                    "Your payment has not been confirmed. Please check its status again later.";

                return RedirectToCheckout(invoiceId);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound &&
                pending.CreatedAt < DateTime.UtcNow.AddHours(-3))
            {
                pending.Status = PaymentTransactionStatus.Expired;
                pending.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await write.CommitAsync();
                TempData["Error"] = "The previous PayPal order has expired. You can choose another payment method.";
                return RedirectToCheckout(invoiceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to check pending PayPal order {OrderId}",
                    pending.OrderId);

                TempData["Error"] =
                    "We couldn't check your previous payment. " +
                    "Please try again later.";

                return RedirectToCheckout(invoiceId);
            }
        }

        // No pending payment exists.
        // Create a new PayPal order.
        var transaction = new PaymentTransaction
        {
            InvoiceId = invoiceId,

            OrderId =
                "INIT-" + Guid.NewGuid().ToString("N"),

            PaymentMethod = PaymentMethod.PayPal,

            RequestedByUserId = userId,

            AmountUSD = balance,
            AmountVND = 0,
            ExchangeRate = 0,

            Status = PaymentTransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            string returnUrl = Url.Action(
                "Return",
                "PayPal",
                null,
                Request.Scheme)!;

            string cancelUrl = Url.Action(
                "Cancel",
                "PayPal",
                null,
                Request.Scheme)!;

            var result = await _paypal.CreateOrderAsync(
                balance,
                invoiceId,
                returnUrl,
                cancelUrl);

            transaction.OrderId = result.OrderId;
            _context.PaymentTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            await write.CommitAsync();

            return Redirect(result.ApprovalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to start PayPal checkout for invoice {InvoiceId}",
                invoiceId);

            TempData["Error"] =
                "We couldn't open PayPal checkout. " +
                "Please try again.";

            return RedirectToCheckout(invoiceId);
        }
    }

    private IActionResult RedirectToCheckout(int invoiceId)
    {
        return RedirectToAction(
            nameof(Checkout),
            new { id = invoiceId });
    }

    private async Task<Invoice?> GetTenantInvoiceAsync(
        int invoiceId)
    {
        string? userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Payments)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i =>
                i.Id == invoiceId &&
                _context.ContractTenants.Any(ct =>
                    ct.ContractId == i.ContractId &&
                    ct.Tenant.ApplicationUserId == userId));
    }
}