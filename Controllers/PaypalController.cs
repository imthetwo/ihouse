using System.Data;
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
public class PayPalController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly PayPalService _paypal;
    private readonly ILogger<PayPalController> _logger;

    public PayPalController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        PayPalService paypal,
        ILogger<PayPalController> logger)
    {
        _db = db;
        _users = users;
        _paypal = paypal;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Return(string? token)
    {
        var transaction = await GetOwnTransactionAsync(token);

        if (transaction == null)
            return NotFound();

        if (transaction.Status == PaymentTransactionStatus.Success)
        {
            TempData["Success"] = "Payment successful!";

            return RedirectToAction(
                "Index",
                "TenantDashboard");
        }

        if (transaction.Status != PaymentTransactionStatus.Pending)
        {
            TempData["Error"] =
                "We couldn't continue your payment. Please contact support.";

            return BackToCheckout(transaction.InvoiceId);
        }

        ViewBag.OrderId = transaction.OrderId;
        ViewBag.InvoiceId = transaction.InvoiceId;
        ViewBag.Amount = transaction.AmountUSD;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Capture(string orderId)
    {
        var transaction = await GetOwnTransactionAsync(orderId);

        if (transaction == null)
            return NotFound();

        if (transaction.Status == PaymentTransactionStatus.Success)
        {
            TempData["Success"] = "Payment successful!";

            return RedirectToAction(
                "Index",
                "TenantDashboard");
        }

        if (transaction.Status != PaymentTransactionStatus.Pending)
        {
            TempData["Error"] =
                "We couldn't verify your payment. Please contact support.";

            return BackToCheckout(transaction.InvoiceId);
        }

        PayPalOrderDetails order;

        try
        {
            order = await _paypal.GetOrderAsync(orderId);

            if (!IsExpectedOrder(order, transaction))
            {
                _logger.LogError(
                    "PayPal order mismatch. Order={OrderId}, Invoice={InvoiceId}",
                    orderId,
                    transaction.InvoiceId);

                TempData["Error"] =
                    "We couldn't verify your payment. Please contact support.";

                return BackToCheckout(transaction.InvoiceId);
            }

            if (order.Status == "CREATED" ||
                order.Status == "PAYER_ACTION_REQUIRED")
            {
                if (!string.IsNullOrWhiteSpace(order.ApprovalUrl))
                {
                    return Redirect(order.ApprovalUrl);
                }

                TempData["Error"] =
                    "Your payment is waiting for approval.";

                return BackToCheckout(transaction.InvoiceId);
            }

            if (order.Status == "APPROVED")
            {
                order = await _paypal.CaptureOrderAsync(orderId);
            }

            if (!IsExpectedOrder(order, transaction))
            {
                _logger.LogError(
                    "PayPal order changed after capture. Order={OrderId}",
                    orderId);

                TempData["Error"] =
                    "We couldn't verify your payment. Please contact support.";

                return BackToCheckout(transaction.InvoiceId);
            }

            if (order.Status == "VOIDED" ||
                order.CaptureStatus is "DECLINED" or "FAILED" or "DENIED")
            {
                var failedTransaction = await _db.PaymentTransactions
                    .FirstOrDefaultAsync(t =>
                        t.Id == transaction.Id &&
                        t.OrderId == orderId &&
                        t.Status == PaymentTransactionStatus.Pending);

                if (failedTransaction != null)
                {
                    failedTransaction.Status = PaymentTransactionStatus.Failed;
                    await _db.SaveChangesAsync();
                }

                TempData["Error"] = "Payment failed. Please try again.";
                return BackToCheckout(transaction.InvoiceId);
            }

            if (order.Status != "COMPLETED" ||
                string.IsNullOrWhiteSpace(order.CaptureId) ||
                !order.CapturedAmount.HasValue)
            {
                _logger.LogWarning(
                    "PayPal capture not confirmed. Order={OrderId}, Status={Status}",
                    orderId,
                    order.Status);

                TempData["Error"] =
                    "We're checking your payment. Please don't pay again.";

                return BackToCheckout(transaction.InvoiceId);
            }

            if (order.CapturedAmount.Value != transaction.AmountUSD)
            {
                _logger.LogError(
                    "PayPal amount mismatch. Order={OrderId}, Expected={Expected}, Actual={Actual}",
                    orderId,
                    transaction.AmountUSD,
                    order.CapturedAmount.Value);

                TempData["Error"] =
                    "Your payment needs review. Please contact support.";

                return BackToCheckout(transaction.InvoiceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "PayPal verification failed. Order={OrderId}",
                orderId);

            TempData["Error"] =
                "We're checking your payment. Please don't pay again.";

            return BackToCheckout(transaction.InvoiceId);
        }

        try
        {
            string captureId = order.CaptureId!;
            decimal amount = order.CapturedAmount!.Value;

            // Compatible with EnableRetryOnFailure in Program.cs.
            var strategy = _db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable);

                var current = await _db.PaymentTransactions
                    .FirstOrDefaultAsync(t =>
                        t.Id == transaction.Id &&
                        t.OrderId == orderId &&
                        t.PaymentMethod == PaymentMethod.PayPal);

                if (current == null)
                {
                    throw new InvalidOperationException(
                        "Payment transaction not found.");
                }

                if (current.Status != PaymentTransactionStatus.Pending &&
                    current.Status != PaymentTransactionStatus.Success)
                {
                    throw new InvalidOperationException(
                        "Payment transaction is not active.");
                }

                var invoice = await _db.Invoices
                    .Include(i => i.Payments)
                    .FirstOrDefaultAsync(i =>
                        i.Id == current.InvoiceId);

                if (invoice == null)
                {
                    throw new InvalidOperationException(
                        "Invoice not found.");
                }

                var existingPayment = await _db.Payments
                    .FirstOrDefaultAsync(p =>
                        p.PaymentMethod == PaymentMethod.PayPal &&
                        p.TransactionCode == captureId);

                decimal paid =
                    invoice.Payments.Sum(p => p.Amount);

                if (existingPayment != null)
                {
                    if (existingPayment.InvoiceId != invoice.Id ||
                        existingPayment.Amount != amount)
                    {
                        throw new InvalidOperationException(
                            "PayPal capture conflicts with another payment.");
                    }
                }
                else
                {
                    if (current.Status == PaymentTransactionStatus.Success)
                    {
                        throw new InvalidOperationException(
                            "Successful transaction has no matching payment.");
                    }

                    decimal remaining =
                        invoice.TotalAmount - paid;

                    if (remaining < amount)
                    {
                        throw new InvalidOperationException(
                            "Invoice balance changed after PayPal capture.");
                    }

                    _db.Payments.Add(new Payment
                    {
                        InvoiceId = invoice.Id,
                        Amount = amount,
                        PaymentDate = DateTime.UtcNow,
                        PaymentMethod = PaymentMethod.PayPal,
                        TransactionCode = captureId
                    });

                    paid += amount;
                }

                current.Status = PaymentTransactionStatus.Success;
                current.CompletedAt ??= DateTime.UtcNow;

                invoice.Status =
                    paid >= invoice.TotalAmount
                        ? InvoiceStatus.Paid
                        : paid > 0
                            ? InvoiceStatus.PartiallyPaid
                            : InvoiceStatus.Unpaid;

                await _db.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            });

            _logger.LogInformation(
                "PayPal payment recorded. Invoice={InvoiceId}, Capture={CaptureId}",
                transaction.InvoiceId,
                captureId);

            TempData["Success"] = "Payment successful!";

            return RedirectToAction(
                "Index",
                "TenantDashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unable to record PayPal payment. Order={OrderId}",
                orderId);

            TempData["Error"] =
                "We're checking your payment. Please don't pay again.";

            return BackToCheckout(transaction.InvoiceId);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Cancel(string? token)
    {
        var transaction = await GetOwnTransactionAsync(token);

        if (transaction == null)
            return NotFound();

        if (transaction.Status == PaymentTransactionStatus.Success)
        {
            TempData["Success"] = "Payment successful!";

            return RedirectToAction(
                "Index",
                "TenantDashboard");
        }

        TempData["Error"] =
            "Payment cancelled. You can continue when ready.";

        return BackToCheckout(transaction.InvoiceId);
    }

    private static bool IsExpectedOrder(
        PayPalOrderDetails order,
        PaymentTransaction transaction)
    {
        return
            order.ReferenceId == $"IHOUSE-{transaction.InvoiceId}" &&
            order.Currency == "USD" &&
            order.OrderAmount == transaction.AmountUSD;
    }

    private RedirectToActionResult BackToCheckout(int invoiceId)
    {
        return RedirectToAction(
            "Checkout",
            "TenantPayments",
            new { id = invoiceId });
    }

    private async Task<PaymentTransaction?> GetOwnTransactionAsync(
        string? orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return null;

        string? userId = _users.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await _db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.OrderId == orderId &&
                t.PaymentMethod == PaymentMethod.PayPal &&
                t.RequestedByUserId == userId &&
                _db.ContractTenants.Any(ct =>
                    ct.ContractId == t.Invoice.ContractId &&
                    ct.Tenant.ApplicationUserId == userId));
    }
}
