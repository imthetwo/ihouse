
using ihouse.Data;
using ihouse.Models;
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

    public InvoicesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Invoices
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var invoices = await _context.Invoices
            .AsNoTracking()
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();

        return View(invoices);
    }

    // GET: /Invoices/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadContractsAsync();
        return View(new InvoiceFormViewModel());
    }

    // POST: /Invoices/Create
    // Not implemented until invoice items and totals are calculated.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(InvoiceFormViewModel model)
    {
        ModelState.AddModelError(
            string.Empty,
            "Invoice generation is not implemented yet.");

        return RedirectToAction(nameof(Index));
    }

    // GET: /Invoices/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
        {
            return NotFound();
        }

        return View(invoice);
    }

    private async Task LoadContractsAsync()
    {
        ViewBag.ContractOptions = await _context.Contracts
            .AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = "Contract #" + c.Id
            })
            .ToListAsync();
    }
}
