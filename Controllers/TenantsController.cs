
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ihouse.Data;
using ihouse.Models;
using ihouse.Models.Enums;
using ihouse.Services;
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
    private readonly EmailService _emailService;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ApplicationDbContext context,
        EmailService emailService,
        ILogger<TenantsController> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
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
    // CREATE TENANT - GET
    // ==========================================

    [HttpGet]
    public IActionResult Create()
    {
        return View(new TenantFormViewModel());
    }

    // ==========================================
    // CREATE TENANT - POST
    // ==========================================

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
    // EDIT TENANT - GET
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

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

    // ==========================================
    // EDIT TENANT - POST
    // ==========================================

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

        bool exists = await _context.Tenants
            .AnyAsync(t =>
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
    // AND SEND EMAIL AUTOMATICALLY
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateInvitation(
        int tenantId,
        int contractId)
    {
        // ======================================
        // 1. FIND TENANT
        // ======================================

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
            return NotFound();

        // ======================================
        // 2. CHECK ACCOUNT LINKING
        // ======================================

        if (!string.IsNullOrWhiteSpace(
            tenant.ApplicationUserId))
        {
            TempData["Error"] =
                "This tenant profile is already linked to an account.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        // ======================================
        // 3. CHECK EMAIL
        // ======================================

        if (string.IsNullOrWhiteSpace(tenant.Email))
        {
            TempData["Error"] =
                "Please add the tenant's email before generating an invitation.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        // ======================================
        // 4. VALIDATE ACTIVE CONTRACT
        // ======================================

        bool validContract = await _context.ContractTenants
            .AsNoTracking()
            .AnyAsync(ct =>
                ct.TenantId == tenantId &&
                ct.ContractId == contractId &&
                ct.Contract.Status == ContractStatus.Active);

        if (!validContract)
        {
            TempData["Error"] =
                "The selected contract is not active or does not belong to this tenant.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        // ======================================
        // 5. GENERATE SECURE CODE
        // ======================================

        var now = DateTime.UtcNow;

        string code = WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(32));

        // ======================================
        // 6. HASH CODE
        // ======================================

        string codeHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(code)));

        // ======================================
        // 7. CREATE INVITATION ENTITY
        // ======================================

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

        // ======================================
        // 8. SAVE INVITATION TO DATABASE
        // ======================================

        try
        {
            _context.TenantInvitations.Add(invitation);

            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Failed to save invitation for Tenant {TenantId}",
                tenantId);

            TempData["Error"] =
                "Unable to create invitation. Please try again.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        // ======================================
        // 9. PREPARE EMAIL CONTENT
        // ======================================

        string subject =
            "iHouse - Contract Invitation Code";

        string body = $"""
            Hello {tenant.FullName},

            Welcome to iHouse!

            Your rental contract is ready.

            Contract ID: {contractId}

            YOUR INVITATION CODE:

            {code}

            ----------------------------------------

            HOW TO CONNECT YOUR CONTRACT

            1. Open the iHouse website.
            2. Register or log in to your account.
            3. Verify your email address.
            4. Go to Tenant Dashboard.
            5. Open Connect Your Contract.
            6. Enter the invitation code above.

            ----------------------------------------

            IMPORTANT INFORMATION

            - This invitation expires in 24 hours.
            - The code can only be used once.
            - Use the same verified email address
              associated with your tenant profile.

            If you did not expect this email,
            please contact your property manager.

            Best regards,
            iHouse Management
            """;

        // ======================================
        // 10. SEND EMAIL AUTOMATICALLY
        // ======================================

        try
        {
            await _emailService.SendEmailAsync(
                tenant.Email!,
                subject,
                body);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send invitation for Tenant {TenantId}",
                tenantId);

            // Invalidate the newly created code
            // if SMTP sending fails.
            try
            {
                await _context.TenantInvitations
                    .Where(i =>
                        i.Id == invitation.Id &&
                        i.RedeemedAt == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            i => i.ExpiresAt,
                            DateTime.UtcNow));
            }
            catch (Exception revokeException)
            {
                _logger.LogError(
                    revokeException,
                    "Could not invalidate invitation {InvitationId}",
                    invitation.Id);
            }

            TempData["Error"] =
                "Invitation email could not be confirmed as sent. " +
                "Please check SMTP settings and server logs.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        // ======================================
        // 11. EXPIRE OLD UNUSED CODES
        // ======================================

        try
        {
            await _context.TenantInvitations
                .Where(i =>
                    i.TenantId == tenantId &&
                    i.Id != invitation.Id &&
                    i.RedeemedAt == null &&
                    i.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        i => i.ExpiresAt,
                        now));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to expire old invitations for Tenant {TenantId}",
                tenantId);

            TempData["Error"] =
                "Email was accepted by SMTP, but old codes " +
                "could not be invalidated. Contact the administrator.";

            return RedirectToAction(
                nameof(Details),
                new { id = tenantId });
        }

        // ======================================
        // 12. SHOW CODE TO ADMIN
        // ======================================

        TempData["InvitationCode"] = code;

        TempData["InvitationTenantId"] = tenantId;

        TempData["Success"] =
            $"Invitation generated and email sent to {tenant.Email}.";

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

    private void ValidateInput(
        TenantFormViewModel model)
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
