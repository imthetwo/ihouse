
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
    private readonly ILogger<TenantDashboardController> _logger;

    public TenantDashboardController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<TenantDashboardController> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    // ==========================================
    // GET: TENANT DASHBOARD
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

        if (tenant == null)
            return RedirectToAction(nameof(Connect));

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
            .Where(c => c.Status == ContractStatus.Active &&
                c.ContractStartDate <= DateOnly.FromDateTime(DateTime.Today) &&
                c.ContractEndDate >= DateOnly.FromDateTime(DateTime.Today))
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
    // GET: CONNECT CONTRACT
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Connect()
    {
        var tenant = await GetCurrentTenantAsync();

        if (tenant != null)
            return RedirectToAction(nameof(Index));

        return View();
    }

    // ==========================================
    // POST: CONNECT CONTRACT
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

        // Already connected
        var existingTenant = await GetCurrentTenantAsync();

        if (existingTenant != null)
            return RedirectToAction(nameof(Index));

        // Generate invitation code hash
        var codeHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(code.Trim())));

        try
        {
            // ======================================
            // EXECUTION STRATEGY
            // ======================================

            var strategy =
                _db.Database.CreateExecutionStrategy();

            string? errorMessage =
                await strategy.ExecuteAsync(async () =>
                {
                    // All operations run inside
                    // one retryable transaction.

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
                        return
                            "Invitation code is invalid, expired, or already used.";
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
                        return
                            "This tenant profile is already linked or unavailable.";
                    }

                    // ==================================
                    // 3. VERIFY EMAIL
                    // ==================================

                    if (!string.Equals(
                            tenant.Email?.Trim(),
                            email,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return
                            "This invitation does not belong to your account.";
                    }

                    // ==================================
                    // 4. CHECK ACCOUNT LINK
                    // ==================================

                    var alreadyLinked = await _db.Tenants
                        .AnyAsync(t =>
                            t.ApplicationUserId == user.Id);

                    if (alreadyLinked)
                    {
                        return
                            "Your account is already linked to a tenant.";
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
                        return
                            "The rental contract is no longer active.";
                    }

                    // ==================================
                    // 6. LINK TENANT ACCOUNT
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
                        return
                            "This tenant profile is no longer available.";
                    }

                    // ==================================
                    // 7. REDEEM INVITATION
                    // ==================================

                    var redeemedRows =
                        await _db.TenantInvitations
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
                        // Transaction will roll back
                        // the tenant link automatically.
                        return
                            "This invitation is no longer available.";
                    }

                    // ==================================
                    // 8. COMMIT TRANSACTION
                    // ==================================

                    await transaction.CommitAsync();

                    return (string?)null;
                });

            // ======================================
            // HANDLE VALIDATION ERRORS
            // ======================================

            if (errorMessage != null)
            {
                ModelState.AddModelError(
                    "",
                    errorMessage);

                return View();
            }

            TempData["Success"] =
                "Contract connected successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (PostgresException ex)
            when (ex.SqlState == "40001" ||
                  ex.SqlState == "40P01" ||
                  ex.SqlState == "23505")
        {
            _logger.LogWarning(
                ex,
                "Contract invitation conflict for user {UserId}",
                user.Id);

            ModelState.AddModelError(
                "",
                "We couldn't complete your request. Please try again.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(
                ex,
                "Failed to connect contract for user {UserId}",
                user.Id);

            ModelState.AddModelError(
                "",
                "Unable to connect your contract. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected contract connection error for user {UserId}",
                user.Id);

            ModelState.AddModelError(
                "",
                "Something went wrong. Please try again later.");
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
        if (string.IsNullOrWhiteSpace(description) ||
            description.Trim().Length > 2000)
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
            .Where(c => c.Status == ContractStatus.Active &&
                c.ContractStartDate <= DateOnly.FromDateTime(DateTime.Today) &&
                c.ContractEndDate >= DateOnly.FromDateTime(DateTime.Today))
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
        await using var write = await _db.BeginWriteAsync(WriteTransaction.Maintenance);
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
        await write.CommitAsync();

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
