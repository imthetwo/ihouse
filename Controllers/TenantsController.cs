
using ihouse.Data;
using ihouse.Models;
using ihouse.ViewModels.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class TenantsController : Controller
{
    private readonly ApplicationDbContext _context;

    public TenantsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Tenants
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenants = await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.FullName)
            .ToListAsync();

        return View(tenants);
    }

    // GET: /Tenants/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new TenantFormViewModel());
    }

    // POST: /Tenants/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TenantFormViewModel model)
    {
        ValidateInput(model);

        if (!ModelState.IsValid)
            return View(model);

        string identity = model.IdentityNumber.Trim();

        bool exists = await _context.Tenants
            .AnyAsync(t => t.IdentityNumber == identity);

        if (exists)
        {
            ModelState.AddModelError(
                nameof(model.IdentityNumber),
                "Identity number already exists.");

            return View(model);
        }

        var tenant = new Tenant
        {
            FullName = model.FullName.Trim(),
            IdentityNumber = identity,
            Phone = Clean(model.Phone),
            Email = Clean(model.Email)
        };

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Tenant created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Tenants/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var tenant = await _context.Tenants.FindAsync(id);

        if (tenant == null)
            return NotFound();

        var model = new TenantFormViewModel
        {
            FullName = tenant.FullName,
            IdentityNumber = tenant.IdentityNumber,
            Phone = tenant.Phone,
            Email = tenant.Email
        };

        return View(model);
    }

    // POST: /Tenants/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        TenantFormViewModel model)
    {
        ValidateInput(model);

        if (!ModelState.IsValid)
            return View(model);

        var tenant = await _context.Tenants.FindAsync(id);

        if (tenant == null)
            return NotFound();

        string identity = model.IdentityNumber.Trim();

        bool exists = await _context.Tenants.AnyAsync(t =>
            t.Id != id &&
            t.IdentityNumber == identity);

        if (exists)
        {
            ModelState.AddModelError(
                nameof(model.IdentityNumber),
                "Identity number already exists.");

            return View(model);
        }

        tenant.FullName = model.FullName.Trim();
        tenant.IdentityNumber = identity;
        tenant.Phone = Clean(model.Phone);
        tenant.Email = Clean(model.Email);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Tenant updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Tenants/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var tenant = await _context.Tenants
            .AsNoTracking()
            .Include(t => t.ContractTenants)
                .ThenInclude(ct => ct.Contract)
                    .ThenInclude(c => c.Room)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant == null)
            return NotFound();

        return View(tenant);
    }

    // POST: /Tenants/Delete/5
    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var tenant = await _context.Tenants.FindAsync(id);

        if (tenant == null)
            return NotFound();

        bool hasContracts = await _context.ContractTenants
            .AnyAsync(ct => ct.TenantId == id);

        bool hasMaintenance = await _context.MaintenanceRequests
            .AnyAsync(m => m.TenantId == id);

        if (hasContracts || hasMaintenance)
        {
            TempData["Error"] =
                "Cannot delete a tenant with related records.";

            return RedirectToAction(nameof(Index));
        }

        _context.Tenants.Remove(tenant);

        try
        {
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tenant deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] =
                "Cannot delete tenant because related records exist.";
        }

        return RedirectToAction(nameof(Index));
    }

    private void ValidateInput(TenantFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.FullName))
        {
            ModelState.AddModelError(
                nameof(model.FullName),
                "Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.IdentityNumber))
        {
            ModelState.AddModelError(
                nameof(model.IdentityNumber),
                "Identity number is required.");
        }
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
