
using System;
using System.Linq;
using System.Threading.Tasks;
using ihouse.Data;
using ihouse.Models;
using ihouse.Models.Enums;
using ihouse.ViewModels.Contract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class ContractsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ContractsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // INDEX
    // GET: /Contracts
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var contracts = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Room)
            .Include(c => c.ContractTenants)
                .ThenInclude(ct => ct.Tenant)
            .OrderByDescending(c => c.ContractStartDate)
            .ToListAsync();

        return View(contracts);
    }

    // ==========================================
    // SEARCH TENANTS
    // GET: /Contracts/SearchTenants?term=...
    //
    // Search by:
    // - Tenant ID
    // - Full Name
    // - Identity Number (CCCD)
    // - Phone Number
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> SearchTenants(string? term)
    {
        term = term?.Trim() ?? string.Empty;

        // Validate search input
        if (string.IsNullOrWhiteSpace(term) ||
            term.Length > 100)
        {
            return Json(Array.Empty<object>());
        }

        // Check whether the input is a valid Tenant ID
        bool isNumber = int.TryParse(
            term,
            out int tenantId
        );

        // Require at least 2 characters for text searches
        if (term.Length < 2 && !isNumber)
        {
            return Json(Array.Empty<object>());
        }

        // Escape special SQL LIKE characters
        string pattern = $"%{EscapeLikePattern(term)}%";

        var tenants = await _context.Tenants
            .AsNoTracking()
            .Where(t =>

                // Search by Tenant ID
                (isNumber && t.Id == tenantId)

                ||

                // Search by Full Name
                EF.Functions.ILike(
                    t.FullName,
                    pattern,
                    "\\"
                )

                ||

                // Search by Identity Number / CCCD
                EF.Functions.ILike(
                    t.IdentityNumber,
                    pattern,
                    "\\"
                )

                ||

                // Search by Phone Number
                (t.Phone != null &&
                 EF.Functions.ILike(
                     t.Phone,
                     pattern,
                     "\\"
                 ))
            )
            .OrderBy(t => t.FullName)
            .Take(10)
            .Select(t => new
            {
                id = t.Id,
                name = t.FullName,
                phone = t.Phone
            })
            .ToListAsync();

        return Json(tenants);
    }

    // ==========================================
    // ESCAPE SQL LIKE PATTERN
    // ==========================================

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    // ==========================================
    // CREATE - GET
    // GET: /Contracts/Create
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadOptions();

        var model = new ContractFormViewModel
        {
            Status = ContractStatus.Pending
        };

        return View(model);
    }

    // ==========================================
    // CREATE - POST
    // POST: /Contracts/Create
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ContractFormViewModel model)
    {
        bool isAdmin = User.IsInRole("Admin");

        // Staff can only create Pending contracts
        if (!isAdmin &&
            model.Status != ContractStatus.Pending)
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "Staff can only create Pending contracts."
            );
        }

        // New contracts cannot be Terminated
        if (model.Status == ContractStatus.Terminated)
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "New contracts cannot be Terminated."
            );
        }

        await ValidateContract(model);

        if (!ModelState.IsValid)
        {
            await LoadOptions(model.PrimaryTenantId);

            return View(model);
        }

        // ======================================
        // CREATE CONTRACT ENTITY
        // ======================================

        var contract = new Contract
        {
            RoomId = model.RoomId,

            ContractStartDate =
                model.ContractStartDate,

            ContractEndDate =
                model.ContractEndDate,

            RentPrice = model.RentPrice,

            DepositAmount = model.DepositAmount,

            PaymentMethod = model.PaymentMethod,

            Status = model.Status
        };

        // ======================================
        // ASSIGN PRIMARY TENANT
        // ======================================

        contract.ContractTenants.Add(
            new ContractTenant
            {
                TenantId = model.PrimaryTenantId,
                IsPrimaryTenant = true
            }
        );

        // ======================================
        // SAVE CONTRACT
        // ======================================

        _context.Contracts.Add(contract);

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Contract created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // EDIT - GET
    // GET: /Contracts/Edit/5
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var contract = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.ContractTenants)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null)
        {
            return NotFound();
        }

        // Terminated contracts cannot be edited
        if (contract.Status == ContractStatus.Terminated)
        {
            TempData["Error"] =
                "Terminated contracts cannot be edited.";

            return RedirectToAction(nameof(Index));
        }

        // Staff cannot edit Active contracts
        if (!User.IsInRole("Admin") &&
            contract.Status != ContractStatus.Pending)
        {
            return Forbid();
        }

        // ======================================
        // MAP CONTRACT TO VIEWMODEL
        // ======================================

        var model = new ContractFormViewModel
        {
            RoomId = contract.RoomId,

            PrimaryTenantId = contract.ContractTenants
                .Where(ct => ct.IsPrimaryTenant)
                .Select(ct => ct.TenantId)
                .FirstOrDefault(),

            ContractStartDate =
                contract.ContractStartDate,

            ContractEndDate =
                contract.ContractEndDate,

            RentPrice = contract.RentPrice,

            DepositAmount = contract.DepositAmount,

            PaymentMethod = contract.PaymentMethod,

            Status = contract.Status
        };

        await LoadOptions(model.PrimaryTenantId);

        return View(model);
    }

    // ==========================================
    // EDIT - POST
    // POST: /Contracts/Edit/5
    // ==========================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ContractFormViewModel model)
    {
        var contract = await _context.Contracts
            .Include(c => c.ContractTenants)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null)
        {
            return NotFound();
        }

        bool isAdmin = User.IsInRole("Admin");

        // ======================================
        // VALIDATE CONTRACT STATUS
        // ======================================

        if (contract.Status == ContractStatus.Terminated)
        {
            TempData["Error"] =
                "Terminated contracts cannot be edited.";

            return RedirectToAction(nameof(Index));
        }

        // Staff may only edit Pending contracts
        if (!isAdmin)
        {
            if (contract.Status != ContractStatus.Pending)
            {
                return Forbid();
            }

            if (model.Status != ContractStatus.Pending)
            {
                ModelState.AddModelError(
                    nameof(model.Status),
                    "Only Admin can activate contracts."
                );
            }
        }

        // Termination must use the Terminate action
        if (model.Status == ContractStatus.Terminated)
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "Use the Terminate action to end a contract."
            );
        }

        // Active contracts cannot return to Pending
        if (contract.Status == ContractStatus.Active &&
            model.Status != ContractStatus.Active)
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "An Active contract cannot return to Pending."
            );
        }

        // ======================================
        // VALIDATE FORM DATA
        // ======================================

        await ValidateContract(model, id);

        if (!ModelState.IsValid)
        {
            await LoadOptions(model.PrimaryTenantId);

            return View(model);
        }

        // ======================================
        // UPDATE PRIMARY TENANT
        // ======================================

        var oldPrimary = contract.ContractTenants
            .FirstOrDefault(ct => ct.IsPrimaryTenant);

        if (oldPrimary != null &&
            oldPrimary.TenantId != model.PrimaryTenantId)
        {
            _context.ContractTenants.Remove(oldPrimary);

            contract.ContractTenants.Remove(oldPrimary);
        }

        var newPrimary = contract.ContractTenants
            .FirstOrDefault(ct =>
                ct.TenantId == model.PrimaryTenantId
            );

        if (newPrimary == null)
        {
            contract.ContractTenants.Add(
                new ContractTenant
                {
                    TenantId = model.PrimaryTenantId,
                    IsPrimaryTenant = true
                }
            );
        }
        else
        {
            newPrimary.IsPrimaryTenant = true;
        }

        // ======================================
        // UPDATE CONTRACT INFORMATION
        // ======================================

        contract.RoomId = model.RoomId;

        contract.ContractStartDate =
            model.ContractStartDate;

        contract.ContractEndDate =
            model.ContractEndDate;

        contract.RentPrice = model.RentPrice;

        contract.DepositAmount =
            model.DepositAmount;

        contract.PaymentMethod =
            model.PaymentMethod;

        contract.Status = model.Status;

        // ======================================
        // SAVE CHANGES
        // ======================================

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Contract updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // DETAILS
    // GET: /Contracts/Details/5
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var contract = await _context.Contracts
            .AsNoTracking()
            .Include(c => c.Room)
            .Include(c => c.ContractTenants)
                .ThenInclude(ct => ct.Tenant)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null)
        {
            return NotFound();
        }

        return View(contract);
    }

    // ==========================================
    // TERMINATE - ADMIN ONLY
    // POST: /Contracts/Terminate/5
    // ==========================================

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Terminate(int id)
    {
        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null)
        {
            return NotFound();
        }

        if (contract.Status == ContractStatus.Terminated)
        {
            TempData["Error"] =
                "Contract is already terminated.";

            return RedirectToAction(nameof(Index));
        }

        contract.Status = ContractStatus.Terminated;

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Contract terminated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // LOAD ROOM AND TENANT OPTIONS
    // ==========================================

    private async Task LoadOptions(
        int? selectedTenantId = null)
    {
        // ======================================
        // LOAD ROOMS
        // ======================================

        var rooms = await _context.Rooms
            .AsNoTracking()
            .OrderBy(r => r.RoomNumber)
            .ToListAsync();

        ViewBag.RoomOptions = new SelectList(
            rooms,
            "Id",
            "RoomNumber"
        );

        // ======================================
        // LOAD SELECTED TENANT
        // ======================================

        ViewBag.SelectedTenantName = "";

        if (selectedTenantId.HasValue &&
            selectedTenantId.Value > 0)
        {
            var tenant = await _context.Tenants
                .AsNoTracking()
                .Where(t =>
                    t.Id == selectedTenantId.Value)
                .Select(t => new
                {
                    t.Id,
                    t.FullName
                })
                .FirstOrDefaultAsync();

            if (tenant != null)
            {
                ViewBag.SelectedTenantName =
                    $"{tenant.FullName} (ID: {tenant.Id})";
            }
        }
    }

    // ==========================================
    // CONTRACT VALIDATION
    // ==========================================

    private async Task ValidateContract(
        ContractFormViewModel model,
        int? currentId = null)
    {
        // ======================================
        // VALIDATE CONTRACT DATES
        // ======================================

        if (model.ContractEndDate <= model.ContractStartDate)
        {
            ModelState.AddModelError(
                nameof(model.ContractEndDate),
                "End date must be after start date."
            );
        }

        // ======================================
        // VALIDATE ROOM
        // ======================================

        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.Id == model.RoomId);

        if (room == null)
        {
            ModelState.AddModelError(
                nameof(model.RoomId),
                "Selected room does not exist."
            );
        }
        else if (
            model.Status == ContractStatus.Active &&
            room.Status == RoomStatus.Maintenance)
        {
            ModelState.AddModelError(
                nameof(model.RoomId),
                "A room under maintenance cannot have an active contract."
            );
        }

        // ======================================
        // VALIDATE PRIMARY TENANT
        // ======================================

        bool tenantExists = await _context.Tenants
            .AnyAsync(t =>
                t.Id == model.PrimaryTenantId);

        if (!tenantExists)
        {
            ModelState.AddModelError(
                nameof(model.PrimaryTenantId),
                "Please select a valid tenant."
            );
        }

        // ======================================
        // VALIDATE OVERLAPPING CONTRACTS
        // ======================================

        if (model.Status == ContractStatus.Active)
        {
            bool overlaps = await _context.Contracts
                .AnyAsync(c =>
                    c.RoomId == model.RoomId &&
                    c.Status == ContractStatus.Active &&
                    (!currentId.HasValue ||
                     c.Id != currentId.Value) &&
                    c.ContractStartDate <= model.ContractEndDate &&
                    c.ContractEndDate >= model.ContractStartDate
                );

            if (overlaps)
            {
                ModelState.AddModelError(
                    nameof(model.RoomId),
                    "This room already has an active contract for these dates."
                );
            }
        }
    }
}
