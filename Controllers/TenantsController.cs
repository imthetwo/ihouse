
using System.Security.Cryptography;
using System.Text;
using ihouse.Data;
using ihouse.Models;
using ihouse.ViewModels.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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

    // ==========================================
    // TENANT LIST
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenants = await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.FullName)
            .ToListAsync();

        return View(tenants);
    }

    // ==========================================
    // CREATE TENANT
    // ==========================================

    [HttpGet]
    public IActionResult Create()
    {
        return View(new TenantFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        TenantFormViewModel model)
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

        TempData["Success"] =
            "Tenant created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // EDIT TENANT
    // ==========================================

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

        TempData["Success"] =
            "Tenant updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // TENANT DETAILS
    // ==========================================

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

    // ==========================================
    // GENERATE CONTRACT INVITATION
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateInvitation(
        int tenantId,
        int contractId)
    {
        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
            return NotFound();

        // A linked tenant cannot be linked again.
        if (!string.IsNullOrEmpty(tenant.ApplicationUserId))
        {
            TempData["Error"] =
                "This tenant profile is already linked to an account.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        // Require a known email so the eventual
        // account can be matched to this tenant.
        if (string.IsNullOrWhiteSpace(tenant.Email))
        {
            TempData["Error"] =
                "Please add the tenant's email before creating an invitation.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        // Verify the contract belongs to this tenant.
        var validContract = await _context.ContractTenants
            .AsNoTracking()
            .AnyAsync(ct =>
                ct.TenantId == tenantId &&
                ct.ContractId == contractId &&
                ct.Contract.Status.ToString() == "Active");

        if (!validContract)
        {
            TempData["Error"] =
                "The selected contract is not an active contract for this tenant.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        var now = DateTime.UtcNow;

        // Expire old unused invitations for this tenant.
        var oldInvitations = await _context.TenantInvitations
            .Where(i =>
                i.TenantId == tenantId &&
                i.RedeemedAt == null &&
                i.ExpiresAt > now)
            .ToListAsync();

        foreach (var old in oldInvitations)
        {
            old.ExpiresAt = now;
        }

        // Create a cryptographically secure token.
        var code = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(32));

        // Store only its SHA-256 hash.
        var codeHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(code)));

        var invitation = new TenantInvitation
        {
            TenantId = tenantId,
            ContractId = contractId,
            CodeHash = codeHash,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24),
            RedeemedAt = null,
            RedeemedByUserId = null
        };

        _context.TenantInvitations.Add(invitation);

        await _context.SaveChangesAsync();

        // Show the original code once after creation.
        TempData["InvitationCode"] = code;
        TempData["InvitationTenantId"] = tenantId;
        TempData["Success"] =
            "Invitation created successfully. Copy the code and share it securely with the tenant.";

        return RedirectToAction(
            nameof(Details),
            new { id = tenantId });
    }

    // ==========================================
    // DELETE TENANT
    // ==========================================

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

        bool hasInvitations = await _context.TenantInvitations
            .AnyAsync(i => i.TenantId == id);

        if (hasContracts || hasMaintenance || hasInvitations)
        {
            TempData["Error"] =
                "Cannot delete a tenant with related records.";

            return RedirectToAction(nameof(Index));
        }

        _context.Tenants.Remove(tenant);

        try
        {
            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Tenant deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] =
                "Cannot delete tenant because related records exist.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // VALIDATION HELPERS
    // ==========================================

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
