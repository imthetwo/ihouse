
using ihouse.Data;
using ihouse.Models.Enums;
using ihouse.Models;
using ihouse.ViewModels.Maintenance;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class MaintenanceController : Controller
{
    private readonly ApplicationDbContext _context;

    public MaintenanceController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Maintenance
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var requests = await _context.MaintenanceRequests
            .AsNoTracking()
            .Include(r => r.Room)
            .Include(r => r.Tenant)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    // GET: /Maintenance/Details/5
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var request = await _context.MaintenanceRequests
            .AsNoTracking()
            .Include(r => r.Room)
            .Include(r => r.Tenant)
            .Include(r => r.MaintenanceCosts)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound();

        return View(request);
    }

    // POST: /Maintenance/Accept/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Maintenance);
        var request = await _context.MaintenanceRequests
            .FindAsync(id);

        if (request == null)
            return NotFound();

        if (request.Status != MaintenanceRqStatus.Pending)
        {
            TempData["Error"] =
                "Only pending requests can be accepted.";

            return RedirectToAction(nameof(Index));
        }

        bool tenantMustApprove = await _context.MaintenanceCosts
            .AnyAsync(c => c.MaintenanceRequestId == id && c.ChargeToTenant && c.Amount > 0);
        if (tenantMustApprove && request.ApprovalStatus != MaintenanceApprovalStatus.Approved)
        {
            TempData["Error"] = "The tenant must approve the quotation before work starts.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = MaintenanceRqStatus.InProgress;

        await _context.SaveChangesAsync();
        await write.CommitAsync();

        TempData["Success"] =
            "Maintenance request accepted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Maintenance/Reject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Maintenance);
        var request = await _context.MaintenanceRequests
            .FindAsync(id);

        if (request == null)
            return NotFound();

        if (request.Status != MaintenanceRqStatus.Pending)
        {
            TempData["Error"] =
                "Only pending requests can be rejected.";

            return RedirectToAction(nameof(Index));
        }

        request.Status = MaintenanceRqStatus.Rejected;
        request.CompletedAt = null;

        await _context.SaveChangesAsync();
        await write.CommitAsync();

        TempData["Success"] =
            "Maintenance request rejected.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Maintenance/Complete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Maintenance);
        var request = await _context.MaintenanceRequests
            .FindAsync(id);

        if (request == null)
            return NotFound();

        if (request.Status != MaintenanceRqStatus.InProgress)
        {
            TempData["Error"] =
                "Only in-progress requests can be completed.";

            return RedirectToAction(nameof(Index));
        }

        request.Status = MaintenanceRqStatus.Completed;
        request.CompletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await write.CommitAsync();

        TempData["Success"] =
            "Maintenance request completed successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadOptions();
        return View(new MaintenanceFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MaintenanceFormViewModel model)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Maintenance);
        await ValidateRequest(model);
        if (!ModelState.IsValid)
        {
            await LoadOptions();
            return View(model);
        }
        _context.MaintenanceRequests.Add(new MaintenanceRequest
        {
            RoomId = model.RoomId,
            TenantId = model.TenantId,
            Description = model.Description.Trim(),
            Status = MaintenanceRqStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        await write.CommitAsync();
        TempData["Success"] = "Maintenance request created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var request = await _context.MaintenanceRequests.FindAsync(id);
        if (request == null) return NotFound();
        if (request.Status != MaintenanceRqStatus.Pending)
        {
            TempData["Error"] = "This request can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }
        await LoadOptions();
        return View(new MaintenanceFormViewModel
        {
            RoomId = request.RoomId, TenantId = request.TenantId,
            Description = request.Description, Status = request.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MaintenanceFormViewModel model)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Maintenance);
        var request = await _context.MaintenanceRequests.Include(r => r.MaintenanceCosts)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != MaintenanceRqStatus.Pending)
        {
            TempData["Error"] = "This request can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (model.Status != MaintenanceRqStatus.Pending)
            ModelState.AddModelError("", "Use Accept, Reject or Complete to change progress.");
        await ValidateRequest(model);
        if (request.MaintenanceCosts.Any() &&
            (model.RoomId != request.RoomId || model.TenantId != request.TenantId))
            ModelState.AddModelError("", "Keep the room and tenant unchanged after adding costs.");
        if (!ModelState.IsValid)
        {
            await LoadOptions();
            return View(model);
        }
        request.RoomId = model.RoomId;
        request.TenantId = model.TenantId;
        request.Description = model.Description.Trim();
        ResetApproval(request);
        await _context.SaveChangesAsync();
        await write.CommitAsync();
        TempData["Success"] = "Maintenance request updated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCost(int id, MaintenanceCostFormViewModel model)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Maintenance);
        var request = await _context.MaintenanceRequests.Include(r => r.MaintenanceCosts)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != MaintenanceRqStatus.Pending)
        {
            TempData["Error"] = "Costs cannot be changed after work starts.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a description and an amount greater than zero with at most two decimal places.";
            return RedirectToAction(nameof(Details), new { id });
        }
        request.MaintenanceCosts.Add(new MaintenanceCost
        {
            Description = model.Description.Trim(), Amount = model.Amount,
            ChargeToTenant = model.ChargeToTenant
        });
        ResetApproval(request);
        await _context.SaveChangesAsync();
        await write.CommitAsync();
        TempData["Success"] = request.ApprovalStatus == MaintenanceApprovalStatus.Pending
            ? "Cost added successfully. Tenant approval is required before work starts."
            : "Cost added successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCost(int id, int costId)
    {
        await using var write = await _context.BeginWriteAsync(WriteTransaction.Maintenance);
        var request = await _context.MaintenanceRequests.Include(r => r.MaintenanceCosts)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return NotFound();
        if (request.Status != MaintenanceRqStatus.Pending)
        {
            TempData["Error"] = "Costs cannot be changed after work starts.";
            return RedirectToAction(nameof(Details), new { id });
        }
        var cost = request.MaintenanceCosts.FirstOrDefault(c => c.Id == costId);
        if (cost == null) return NotFound();
        request.MaintenanceCosts.Remove(cost);
        _context.MaintenanceCosts.Remove(cost);
        ResetApproval(request);
        await _context.SaveChangesAsync();
        await write.CommitAsync();
        TempData["Success"] = "Cost removed successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static void ResetApproval(MaintenanceRequest request)
    {
        request.ApprovalStatus = request.MaintenanceCosts.Any(c => c.ChargeToTenant && c.Amount > 0)
            ? MaintenanceApprovalStatus.Pending : MaintenanceApprovalStatus.NotRequired;
        request.ApprovalDecidedAt = null;
    }

    private async Task ValidateRequest(MaintenanceFormViewModel model)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        bool valid = await _context.ContractTenants.AnyAsync(ct =>
            ct.TenantId == model.TenantId && ct.Contract.RoomId == model.RoomId &&
            ct.Contract.Status == ContractStatus.Active &&
            ct.Contract.ContractStartDate <= today && ct.Contract.ContractEndDate >= today);
        if (!valid) ModelState.AddModelError("", "Select a tenant with a current active contract for this room.");
    }

    private async Task LoadOptions()
    {
        ViewBag.RoomOptions = new SelectList(await _context.Rooms.OrderBy(r => r.RoomNumber).ToListAsync(), "Id", "RoomNumber");
        ViewBag.TenantOptions = new SelectList(await _context.Tenants.OrderBy(t => t.FullName).ToListAsync(), "Id", "FullName");
    }
}
