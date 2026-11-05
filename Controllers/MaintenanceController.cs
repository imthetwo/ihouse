
using ihouse.Data;
using ihouse.Models.Enums;
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

        request.Status = MaintenanceRqStatus.InProgress;

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Maintenance request accepted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Maintenance/Reject/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
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

        TempData["Success"] =
            "Maintenance request rejected.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /Maintenance/Complete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
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

        TempData["Success"] =
            "Maintenance request completed successfully.";

        return RedirectToAction(nameof(Index));
    }
}
