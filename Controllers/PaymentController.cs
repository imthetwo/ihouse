
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

    public PaymentController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Payment
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var payments = await _context.Payments
            .AsNoTracking()
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        return View(payments);
    }

    // GET: /Payment/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadInvoicesAsync();

        return View(new PaymentFormViewModel());
    }

    // POST: /Payment/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        PaymentFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadInvoicesAsync();
            return View(model);
        }

        // Prevent automatic recording of unverified MoMo payments.
        if (model.PaymentMethod == PaymentMethod.MoMo)
        {
            ModelState.AddModelError(
                nameof(model.PaymentMethod),
                "MoMo payments must be verified through the payment gateway.");

            await LoadInvoicesAsync();
            return View(model);
        }

        // Load invoice and existing payments
        var invoice = await _context.Invoices
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == model.InvoiceId);

        if (invoice == null)
        {
            ModelState.AddModelError(
                nameof(model.InvoiceId),
                "Selected invoice does not exist.");

            await LoadInvoicesAsync();
            return View(model);
        }

        decimal totalPaid = invoice.Payments.Sum(p => p.Amount);
        decimal remainingBalance = invoice.TotalAmount - totalPaid;

        if (remainingBalance <= 0)
        {
            ModelState.AddModelError(
                nameof(model.InvoiceId),
                "This invoice has already been fully paid.");

            await LoadInvoicesAsync();
            return View(model);
        }

        if (model.Amount > remainingBalance)
        {
            ModelState.AddModelError(
                nameof(model.Amount),
                $"Payment cannot exceed the remaining balance of ${remainingBalance:N2}.");

            await LoadInvoicesAsync();
            return View(model);
        }

        // Avoid incorrectly treating browser-local time as UTC.
        // This project stores PaymentDate using UTC.
        DateTime paymentDate = model.PaymentDate;

        if (paymentDate.Kind == DateTimeKind.Unspecified)
        {
            var vietnamTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById(
                    OperatingSystem.IsWindows()
                        ? "SE Asia Standard Time"
                        : "Asia/Ho_Chi_Minh");

            paymentDate = TimeZoneInfo.ConvertTimeToUtc(
                paymentDate,
                vietnamTimeZone);
        }
        else
        {
            paymentDate = paymentDate.ToUniversalTime();
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Amount = model.Amount,
            PaymentDate = paymentDate,
            PaymentMethod = model.PaymentMethod,
            TransactionCode = model.TransactionCode
        };

        _context.Payments.Add(payment);

        decimal newTotalPaid = totalPaid + model.Amount;

        invoice.Status = newTotalPaid >= invoice.TotalAmount
            ? InvoiceStatus.Paid
            : InvoiceStatus.PartiallyPaid;

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = "Payment recorded successfully.";

        return RedirectToAction(nameof(Index));
    }

    // LOAD INVOICE DROPDOWN
    private async Task LoadInvoicesAsync()
    {
        ViewBag.InvoiceOptions = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Paid)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new SelectListItem
            {
                Value = i.Id.ToString(),
                Text = "Invoice #" + i.Id +
                       " - Contract #" + i.ContractId
            })
            .ToListAsync();
    }
}
