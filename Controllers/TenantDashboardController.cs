using System.Data;
using System.Security.Cryptography;
using System.Text;
using ihouse.Data;
using ihouse.Models;
using ihouse.Models.Enums;
using ihouse.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ihouse.Controllers;

[Authorize(Roles = "Tenant")]
public class TenantDashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantDashboardController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // ==========================================
    // GET: /TenantDashboard
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.ApplicationUserId == userId);

        // Not linked -> Connect first
        if (tenant == null)
        {
            return RedirectToAction(nameof(Connect));
        }

        // Linked -> Real Dashboard
        var model = new TenantDashboardViewModel
        {
            TenantLinked = true,
            TenantName = tenant.FullName
        };

        // ======================================
        // CONTRACTS AND ROOM
        // ======================================

        var contractLinks = await _db.ContractTenants
            .AsNoTracking()
            .Include(ct => ct.Contract)
                .ThenInclude(c => c.Room)
            .Where(ct => ct.TenantId == tenant.Id)
            .ToListAsync();

        var contracts = contractLinks
            .Select(ct => ct.Contract)
            .DistinctBy(c => c.Id)
            .ToList();

        var currentContract = contracts
            .Where(c => c.Status.ToString() == "Active")
            .OrderByDescending(c => c.ContractStartDate)
            .FirstOrDefault();

        if (currentContract != null)
        {
            var room = currentContract.Room;

            model.Room = new RoomInfo
            {
                Id = room.Id,
                RoomNumber = room.RoomNumber,
                Floor = room.Floor,
                RoomType = room.RoomType,
                Capacity = room.Capacity,
                Description = room.Description,
                ImageUrl = room.ImageUrl
            };

            model.Contract = new ContractInfo
            {
                Id = currentContract.Id,
                RoomId = currentContract.RoomId,
                StartDate = currentContract.ContractStartDate,
                EndDate = currentContract.ContractEndDate,
                MonthlyRent = currentContract.RentPrice,
                DepositAmount = currentContract.DepositAmount,
                Status = currentContract.Status.ToString(),
                PaymentMethod =
                    currentContract.PaymentMethod.ToString()
            };
        }

        // ======================================
        // INVOICES
        // ======================================

        var contractIds = contracts
            .Select(c => c.Id)
            .ToList();

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Where(i => contractIds.Contains(i.ContractId))
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .ToListAsync();

        model.Invoices = invoices.Select(i =>
        {
            var paid = i.Payments.Sum(p => p.Amount);

            return new InvoiceInfo
            {
                Id = i.Id,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                TotalAmount = i.TotalAmount,
                PaidAmount = paid,
                Status = i.Status.ToString(),

                Items = i.InvoiceItems
                    .OrderBy(item => item.Id)
                    .Select(item => new InvoiceItemInfo
                    {
                        ItemType = item.ItemType.ToString(),
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Amount = item.Amount
                    })
                    .ToList()
            };
        }).ToList();

        var payableInvoices = model.Invoices
            .Where(i =>
                !string.Equals(
                    i.Status,
                    "Cancelled",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !string.Equals(
                    i.Status,
                    "Canceled",
                    StringComparison.OrdinalIgnoreCase)
                &&
                !string.Equals(
                    i.Status,
                    "Void",
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        model.OutstandingBalance =
            payableInvoices.Sum(i => i.Balance);

        model.NextDueDate = payableInvoices
            .Where(i => i.Balance > 0)
            .OrderBy(i => i.DueDate)
            .Select(i => (DateOnly?)i.DueDate)
            .FirstOrDefault();

        // ======================================
        // PAYMENTS
        // ======================================

        model.Payments = invoices
            .SelectMany(i => i.Payments.Select(p =>
                new PaymentInfo
                {
                    Id = p.Id,
                    InvoiceId = i.Id,
                    Amount = p.Amount,
                    PaymentDate = p.PaymentDate,
                    PaymentMethod =
                        p.PaymentMethod.ToString(),
                    TransactionCode = p.TransactionCode
                }))
            .OrderByDescending(p => p.PaymentDate)
            .ToList();

        // ======================================
        // MAINTENANCE
        // ======================================

        var maintenanceRequests =
            await _db.MaintenanceRequests
                .AsNoTracking()
                .Include(m => m.MaintenanceCosts)
                .Where(m => m.TenantId == tenant.Id)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

        model.MaintenanceRequests = maintenanceRequests
            .Select(m => new MaintenanceInfo
            {
                Id = m.Id,
                RoomId = m.RoomId,
                Description = m.Description,
                Status = m.Status.ToString(),
                ApprovalStatus =
                    m.ApprovalStatus.ToString(),
                CreatedAt = m.CreatedAt,
                CompletedAt = m.CompletedAt,

                ProposedTenantCost = m.MaintenanceCosts
                    .Where(c => c.ChargeToTenant)
                    .Sum(c => c.Amount),

                Costs = m.MaintenanceCosts
                    .OrderBy(c => c.Id)
                    .Select(c => new MaintenanceCostInfo
                    {
                        Id = c.Id,
                        Description = c.Description,
                        Amount = c.Amount,
                        ChargeToTenant = c.ChargeToTenant
                    })
                    .ToList()
            })
            .ToList();

        return View(model);
    }

    // ==========================================
    // GET: /TenantDashboard/Connect
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Connect()
    {
        var tenant = await GetCurrentTenantAsync();

        // Already linked -> Dashboard
        if (tenant != null)
            return RedirectToAction(nameof(Index));

        return View();
    }

    // ==========================================
    // POST: /TenantDashboard/Connect
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Connect(string? code)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            ModelState.AddModelError(
                "",
                "Please verify your email before connecting.");

            return View();
        }

        if (string.IsNullOrWhiteSpace(code) ||
            code.Trim().Length > 128)
        {
            ModelState.AddModelError(
                "",
                "Please enter a valid invitation code.");

            return View();
        }

        var email = user.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(
                "",
                "Your account does not have a valid email.");

            return View();
        }

        // Generate the same SHA-256 hash used
        // by TenantsController.GenerateInvitation
        var codeHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(code.Trim())));

        try
        {
            // All redemption changes must be atomic.
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            var now = DateTime.UtcNow;

            // ==================================
            // 1. VALIDATE INVITATION
            // ==================================

            var invitation = await _db.TenantInvitations
                .AsNoTracking()
                .FirstOrDefaultAsync(i =>
                    i.CodeHash == codeHash &&
                    i.RedeemedAt == null &&
                    i.ExpiresAt > now);

            if (invitation == null)
            {
                ModelState.AddModelError(
                    "",
                    "Invitation code is invalid, expired, or already used.");

                return View();
            }

            // ==================================
            // 2. VALIDATE TENANT
            // ==================================

            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.Id == invitation.TenantId);

            if (tenant == null ||
                !string.IsNullOrWhiteSpace(
                    tenant.ApplicationUserId))
            {
                ModelState.AddModelError(
                    "",
                    "This tenant profile is already linked or unavailable.");

                return View();
            }

            // ==================================
            // 3. MATCH VERIFIED EMAIL
            // ==================================

            if (!string.Equals(
                    tenant.Email?.Trim(),
                    email,
                    StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    "",
                    "This invitation does not belong to your account.");

                return View();
            }

            // ==================================
            // 4. CHECK EXISTING ACCOUNT LINK
            // ==================================

            var alreadyLinked = await _db.Tenants
                .AnyAsync(t =>
                    t.ApplicationUserId == user.Id);

            if (alreadyLinked)
            {
                ModelState.AddModelError(
                    "",
                    "Your account is already linked to a tenant.");

                return View();
            }

            // ==================================
            // 5. VALIDATE CONTRACT
            // ==================================

            var contractLink = await _db.ContractTenants
                .AsNoTracking()
                .Include(ct => ct.Contract)
                .FirstOrDefaultAsync(ct =>
                    ct.TenantId == invitation.TenantId &&
                    ct.ContractId == invitation.ContractId);

            if (contractLink == null ||
                contractLink.Contract.Status.ToString()
                    != "Active")
            {
                ModelState.AddModelError(
                    "",
                    "The rental contract is no longer active.");

                return View();
            }

            // ==================================
            // 6. LINK ACCOUNT TO TENANT
            // ==================================

            var linkedRows = await _db.Tenants
                .Where(t =>
                    t.Id == tenant.Id &&
                    t.ApplicationUserId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        t => t.ApplicationUserId,
                        user.Id));

            if (linkedRows != 1)
            {
                ModelState.AddModelError(
                    "",
                    "This tenant profile is no longer available.");

                return View();
            }

            // ==================================
            // 7. REDEEM INVITATION ONCE
            // ==================================

            var redeemedRows = await _db.TenantInvitations
                .Where(i =>
                    i.Id == invitation.Id &&
                    i.RedeemedAt == null &&
                    i.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        i => i.RedeemedAt,
                        (DateTime?)now)
                    .SetProperty(
                        i => i.RedeemedByUserId,
                        user.Id));

            if (redeemedRows != 1)
            {
                ModelState.AddModelError(
                    "",
                    "This invitation is no longer available.");

                return View();
            }

            // Both operations succeeded.
            await transaction.CommitAsync();

            TempData["Success"] =
                "Contract connected successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex)
            when (ex.SqlState == "40001" ||
                  ex.SqlState == "40P01" ||
                  ex.SqlState == "23505")
        {
            ModelState.AddModelError(
                "",
                "The invitation could not be processed. Please try again.");
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                "",
                "Unable to connect the contract. Please try again.");
        }

        return View();
    }

    // ==========================================
    // POST: CREATE MAINTENANCE REQUEST
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMaintenanceRequest(
        string description)
    {
        if (string.IsNullOrWhiteSpace(description)
            || description.Trim().Length > 2000)
        {
            TempData["Error"] =
                "Please enter an issue description (max 2000 characters).";

            return RedirectToAction(nameof(Index));
        }

        var tenant = await GetCurrentTenantAsync();

        if (tenant == null)
            return Forbid();

        var activeLinks = await _db.ContractTenants
            .AsNoTracking()
            .Include(ct => ct.Contract)
            .Where(ct => ct.TenantId == tenant.Id)
            .ToListAsync();

        var activeContract = activeLinks
            .Select(ct => ct.Contract)
            .Where(c => c.Status.ToString() == "Active")
            .OrderByDescending(c => c.ContractStartDate)
            .FirstOrDefault();

        if (activeContract == null)
        {
            TempData["Error"] =
                "No active rental contract was found.";

            return RedirectToAction(nameof(Index));
        }

        var request = new MaintenanceRequest
        {
            TenantId = tenant.Id,
            RoomId = activeContract.RoomId,
            Description = description.Trim(),
            Status = MaintenanceRqStatus.Pending,
            ApprovalStatus =
                MaintenanceApprovalStatus.NotRequired,
            CreatedAt = DateTime.UtcNow
        };

        _db.MaintenanceRequests.Add(request);
        await _db.SaveChangesAsync();

        TempData["Success"] =
            "Maintenance request submitted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // POST: APPROVE MAINTENANCE
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveMaintenance(int id)
    {
        return await DecideMaintenanceAsync(id, true);
    }

    // ==========================================
    // POST: REJECT MAINTENANCE
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectMaintenance(int id)
    {
        return await DecideMaintenanceAsync(id, false);
    }

    // ==========================================
    // SHARED MAINTENANCE APPROVAL
    // ==========================================

    private async Task<IActionResult> DecideMaintenanceAsync(
        int id,
        bool approve)
    {
        var tenant = await GetCurrentTenantAsync();

        if (tenant == null)
            return Forbid();

        var request = await _db.MaintenanceRequests
            .Include(m => m.MaintenanceCosts)
            .FirstOrDefaultAsync(m =>
                m.Id == id &&
                m.TenantId == tenant.Id);

        if (request == null)
            return NotFound();

        if (request.ApprovalStatus !=
            MaintenanceApprovalStatus.Pending)
        {
            TempData["Error"] =
                "This quotation is no longer awaiting approval.";

            return RedirectToAction(nameof(Index));
        }

        if (request.Status.ToString() != "Pending")
        {
            TempData["Error"] =
                "This maintenance request can no longer be approved.";

            return RedirectToAction(nameof(Index));
        }

        var tenantCost = request.MaintenanceCosts
            .Where(c => c.ChargeToTenant)
            .Sum(c => c.Amount);

        if (tenantCost <= 0)
        {
            TempData["Error"] =
                "There is no tenant quotation to approve.";

            return RedirectToAction(nameof(Index));
        }

        request.ApprovalStatus = approve
            ? MaintenanceApprovalStatus.Approved
            : MaintenanceApprovalStatus.Rejected;

        request.ApprovalDecidedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Success"] = approve
            ? "Quotation approved. Staff can now arrange the repair."
            : "Quotation rejected. Staff may revise the proposal.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // GET CURRENT TENANT
    // ==========================================

    private async Task<Tenant?> GetCurrentTenantAsync()
    {
        var userId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.ApplicationUserId == userId);
    }
}
