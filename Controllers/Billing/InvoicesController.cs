
using System.Data;
using ihouse.Data;
using ihouse.Models;
using ihouse.Services;
using ihouse.Models.Enums;
using ihouse.ViewModels.Invoice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class InvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(
        ApplicationDbContext context,
        ILogger<InvoicesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ==========================================
    // 1. INVOICE LIST
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var invoices = await _context.Invoices
            .AsNoTracking()
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .ToListAsync();

        return View(invoices);
    }

    // ==========================================
    // 2. CREATE INVOICE - GET
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadContractsAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);

        return View(new InvoiceFormViewModel
        {
            InvoiceDate = today,
            DueDate = today.AddDays(7)
        });
    }

    // ==========================================
    // 3. GET CONTRACT PERIOD
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> ContractPeriod(int id)
    {
        var contract = await _context.Contracts
            .AsNoTracking()
            .Where(c =>
                c.Id == id &&
                c.Status == ContractStatus.Active)
            .Select(c => new
            {
                c.Id,
                c.ContractStartDate,
                c.ContractEndDate,
                c.RentPrice,
                RoomNumber = c.Room.RoomNumber
            })
            .FirstOrDefaultAsync();

        if (contract == null)
        {
            return NotFound();
        }

        return Json(new
        {
            contractId = contract.Id,
            startDate = contract.ContractStartDate
                .ToString("yyyy-MM-dd"),
            endDate = contract.ContractEndDate
                .ToString("yyyy-MM-dd"),
            monthlyRent = contract.RentPrice,
            roomNumber = contract.RoomNumber
        });
    }

    // ==========================================
    // 4. CREATE INVOICE - POST
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        InvoiceFormViewModel model)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Invoices);
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(73421, {WriteTransaction.Meters})");
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(73421, {WriteTransaction.Maintenance})");
        if (model.ContractId <= 0)
        {
            ModelState.AddModelError(
                nameof(model.ContractId),
                "Please select a rental contract.");
        }

        if (model.DueDate < model.InvoiceDate)
        {
            ModelState.AddModelError(
                nameof(model.DueDate),
                "Due date cannot be before invoice date.");
        }

        var contract = await _context.Contracts
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == model.ContractId);

        if (contract == null)
        {
            ModelState.AddModelError(
                nameof(model.ContractId),
                "Contract not found.");
        }
        else
        {
            if (contract.Status != ContractStatus.Active)
            {
                ModelState.AddModelError(
                    nameof(model.ContractId),
                    "Only active contracts can be invoiced.");
            }

            if (contract.RentPrice <= 0)
            {
                ModelState.AddModelError(
                    nameof(model.ContractId),
                    "Contract rent must be greater than zero.");
            }

            if (model.InvoiceDate <
                    contract.ContractStartDate ||
                model.InvoiceDate >
                    contract.ContractEndDate)
            {
                ModelState.AddModelError(
                    nameof(model.InvoiceDate),
                    "Invoice date must be within the contract period.");
            }
        }

        if (contract != null &&
            model.InvoiceDate != default)
        {
            var monthStart = new DateOnly(
                model.InvoiceDate.Year,
                model.InvoiceDate.Month,
                1);

            var nextMonth = monthStart.AddMonths(1);

            bool exists = await _context.Invoices
                .AnyAsync(i =>
                    i.ContractId == model.ContractId &&
                    i.InvoiceDate >= monthStart &&
                    i.InvoiceDate < nextMonth);

            if (exists)
            {
                ModelState.AddModelError(
                    nameof(model.InvoiceDate),
                    "An invoice already exists for this " +
                    "contract and month.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadContractsAsync();
            return View(model);
        }

        var invoice = new Invoice
        {
            ContractId = model.ContractId,
            InvoiceDate = model.InvoiceDate,
            DueDate = model.DueDate,
            TotalAmount = contract!.RentPrice,
            Status = InvoiceStatus.Unpaid,

            InvoiceItems = new List<InvoiceItem>
            {
                new InvoiceItem
                {
                    ItemType = InvoiceItemType.Rent,
                    Description = "Monthly rental fee",
                    Quantity = 1,
                    UnitPrice = contract.RentPrice,
                    Amount = contract.RentPrice
                }
            }
        };

        var billingError = await InvoiceCalculator.AddServicesAsync(_context, contract, invoice);
        if (billingError != null)
        {
            ModelState.AddModelError("", billingError);
            await LoadContractsAsync();
            return View(model);
        }
        _context.Invoices.Add(invoice);

        await _context.SaveChangesAsync();
        await write.CommitAsync();

        TempData["Success"] =
            $"Invoice #{invoice.Id} created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // 5. INVOICE DETAILS
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Contract)
                .ThenInclude(c => c.Room)
            .Include(i => i.Contract)
                .ThenInclude(c => c.ContractTenants)
                    .ThenInclude(ct => ct.Tenant)
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
        {
            return NotFound();
        }

        return View(invoice);
    }

    // ==========================================
    // 6. EDIT INVOICE - GET
    // ADMIN ONLY
    // ==========================================

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
        {
            return NotFound();
        }

        if (await HasPaymentHistoryAsync(id))
        {
            TempData["Error"] =
                "This invoice has payment activity and " +
                "cannot be edited.";

            return RedirectToAction(nameof(Index));
        }

        var rentItems = invoice.InvoiceItems
            .Where(i => i.ItemType == InvoiceItemType.Rent)
            .ToList();

        if (rentItems.Count != 1)
        {
            TempData["Error"] =
                "This invoice cannot be edited right now. Please try again later.";

            return RedirectToAction(nameof(Index));
        }

        var model = new InvoiceEditViewModel
        {
            Id = invoice.Id,
            ContractId = invoice.ContractId,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            RentAmount = rentItems[0].Amount
        };

        return View(model);
    }

    // ==========================================
    // 7. EDIT INVOICE - POST
    // ADMIN ONLY
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(
        int id,
        InvoiceEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (model.InvoiceDate == default)
        {
            ModelState.AddModelError(
                nameof(model.InvoiceDate),
                "Invoice date is required.");
        }

        if (model.DueDate == default ||
            model.DueDate < model.InvoiceDate)
        {
            ModelState.AddModelError(
                nameof(model.DueDate),
                "Due date must be on or after invoice date.");
        }

        if (decimal.Round(model.RentAmount, 2) !=
            model.RentAmount)
        {
            ModelState.AddModelError(
                nameof(model.RentAmount),
                "Use a maximum of two decimal places.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await using var transaction =
                await _context.BeginWriteAsync(WriteTransaction.Invoices);

            var invoice = await _context.Invoices
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            if (await HasPaymentHistoryAsync(id))
            {
                TempData["Error"] =
                    "Invoice has payment activity. " +
                    "Editing is not allowed.";

                return RedirectToAction(nameof(Index));
            }

            var contract = await _context.Contracts
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.Id == invoice.ContractId);

            if (contract == null)
            {
                ModelState.AddModelError(
                    "",
                    "The related contract was not found.");
            }
            else if (
                model.InvoiceDate <
                    contract.ContractStartDate ||
                model.InvoiceDate >
                    contract.ContractEndDate)
            {
                ModelState.AddModelError(
                    nameof(model.InvoiceDate),
                    "Invoice date must be within " +
                    "the contract period.");
            }

            var monthStart = new DateOnly(
                model.InvoiceDate.Year,
                model.InvoiceDate.Month,
                1);

            var nextMonth = monthStart.AddMonths(1);

            bool duplicate = await _context.Invoices
                .AnyAsync(i =>
                    i.Id != invoice.Id &&
                    i.ContractId == invoice.ContractId &&
                    i.InvoiceDate >= monthStart &&
                    i.InvoiceDate < nextMonth);

            if (duplicate)
            {
                ModelState.AddModelError(
                    nameof(model.InvoiceDate),
                    "Another invoice already exists " +
                    "for this contract and month.");
            }

            var rentItems = invoice.InvoiceItems
                .Where(i =>
                    i.ItemType == InvoiceItemType.Rent)
                .ToList();

            if (rentItems.Count != 1)
            {
                ModelState.AddModelError(
                    "",
                    "This invoice cannot be updated right now. Please try again later.");
            }

            if (!ModelState.IsValid)
            {
                model.ContractId = invoice.ContractId;
                return View(model);
            }

            if (invoice.InvoiceItems.Any(i => i.ItemType != InvoiceItemType.Rent) &&
                (invoice.InvoiceDate.Year != model.InvoiceDate.Year || invoice.InvoiceDate.Month != model.InvoiceDate.Month))
            {
                ModelState.AddModelError("", "An invoice with service charges must stay in its original month.");
                model.ContractId = invoice.ContractId;
                return View(model);
            }
            var rentItem = rentItems[0];

            rentItem.Quantity = 1;
            rentItem.UnitPrice = model.RentAmount;
            rentItem.Amount = model.RentAmount;

            invoice.InvoiceDate = model.InvoiceDate;
            invoice.DueDate = model.DueDate;

            invoice.TotalAmount =
                invoice.InvoiceItems.Sum(i => i.Amount);

            invoice.Status = InvoiceStatus.Unpaid;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] =
                $"Invoice #{invoice.Id} updated successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id = invoice.Id });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Database error updating invoice {InvoiceId}",
                id);

            ModelState.AddModelError(
                "",
                "We couldn't update the invoice. Please try again.");

            return View(model);
        }
    }

    // ==========================================
    // 8. DELETE INVOICE - GET
    // ADMIN ONLY
    // ==========================================

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
        {
            return NotFound();
        }

        if (await HasPaymentHistoryAsync(id))
        {
            TempData["Error"] =
                "Invoice cannot be deleted because " +
                "payment activity exists.";

            return RedirectToAction(nameof(Index));
        }

        return View(invoice);
    }

    // ==========================================
    // 9. DELETE INVOICE - POST
    // ADMIN ONLY
    // ==========================================

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await using var transaction =
                await _context.BeginWriteAsync(WriteTransaction.Invoices);

            var invoice = await _context.Invoices
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return NotFound();
            }

            if (await HasPaymentHistoryAsync(id))
            {
                TempData["Error"] =
                    "Invoice cannot be deleted because " +
                    "payment activity exists.";

                return RedirectToAction(nameof(Index));
            }

            await _context.MaintenanceRequests.Where(r => r.BilledInvoiceId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.BilledInvoiceId, (int?)null));

            // Remove invoice items before the invoice.
            _context.InvoiceItems.RemoveRange(
                invoice.InvoiceItems);

            _context.Invoices.Remove(invoice);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Success"] =
                $"Invoice #{id} deleted successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Unable to delete invoice {InvoiceId}",
                id);

            TempData["Error"] =
                "This invoice cannot be deleted right now. Please try again later.";

            return RedirectToAction(nameof(Index));
        }
    }

    // ==========================================
    // 10. PAYMENT SAFETY CHECK
    // ==========================================

    private async Task<bool> HasPaymentHistoryAsync(
        int invoiceId)
    {
        bool hasPayments = await _context.Payments
            .AnyAsync(p => p.InvoiceId == invoiceId);

        if (hasPayments)
        {
            return true;
        }

        // Includes Pending, Success, Failed and any
        // other transaction statuses.
        return await _context.PaymentTransactions
            .AnyAsync(t => t.InvoiceId == invoiceId);
    }

    // ==========================================
    // 11. LOAD ACTIVE CONTRACTS
    // ==========================================

    private async Task LoadContractsAsync()
    {
        ViewBag.ContractOptions = await _context.Contracts
            .AsNoTracking()
            .Where(c =>
                c.Status == ContractStatus.Active)
            .OrderByDescending(c => c.Id)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = "Contract #" + c.Id +
                       " - Room " + c.Room.RoomNumber
            })
            .ToListAsync();
    }
}
