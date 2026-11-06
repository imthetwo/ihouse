
using ihouse.Data;
using ihouse.Models;
using ihouse.ViewModels.ServicePrice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class ServicePriceController : Controller
{
    private readonly ApplicationDbContext _context;

    public ServicePriceController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // INDEX - ADMIN & STAFF
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var prices = await _context.ServicePrices
            .AsNoTracking()
            .OrderBy(p => p.ServiceType)
            .ThenByDescending(p => p.EffectiveFrom)
            .ToListAsync();

        return View(prices);
    }

    // ==========================================
    // CREATE - ADMIN ONLY
    // ==========================================

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new ServicePriceFormViewModel());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ServicePriceFormViewModel model)
    {
        await ValidatePricePeriod(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var servicePrice = new ServicePrice
        {
            ServiceType = model.ServiceType,
            Price = model.Price,
            EffectiveFrom = model.EffectiveFrom,
            EffectiveTo = model.EffectiveTo,
            IsActive = model.IsActive
        };

        _context.ServicePrices.Add(servicePrice);

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Service price created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // EDIT - ADMIN ONLY
    // ==========================================

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var price = await _context.ServicePrices
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (price == null)
        {
            return NotFound();
        }

        var model = new ServicePriceFormViewModel
        {
            ServiceType = price.ServiceType,
            Price = price.Price,
            EffectiveFrom = price.EffectiveFrom,
            EffectiveTo = price.EffectiveTo,
            IsActive = price.IsActive
        };

        return View(model);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ServicePriceFormViewModel model)
    {
        var price = await _context.ServicePrices
            .FirstOrDefaultAsync(p => p.Id == id);

        if (price == null)
        {
            return NotFound();
        }

        // Protect prices already linked to rooms.
        bool isLinked = await _context.RoomServices
            .AnyAsync(rs => rs.ServicePriceId == id);

        if (isLinked)
        {
            bool pricingChanged =
                price.ServiceType != model.ServiceType ||
                price.Price != model.Price ||
                price.EffectiveFrom != model.EffectiveFrom ||
                price.EffectiveTo != model.EffectiveTo;

            if (pricingChanged)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This service price is linked to rooms. " +
                    "You may change its active status, " +
                    "but cannot modify its type, price or dates."
                );
            }
        }

        await ValidatePricePeriod(model, id);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        price.ServiceType = model.ServiceType;
        price.Price = model.Price;
        price.EffectiveFrom = model.EffectiveFrom;
        price.EffectiveTo = model.EffectiveTo;
        price.IsActive = model.IsActive;

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Service price updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // DELETE - ADMIN ONLY
    // ==========================================

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var price = await _context.ServicePrices
            .FirstOrDefaultAsync(p => p.Id == id);

        if (price == null)
        {
            return NotFound();
        }

        bool isLinked = await _context.RoomServices
            .AnyAsync(rs => rs.ServicePriceId == id);

        if (isLinked)
        {
            TempData["Error"] =
                "Cannot delete a service price linked to rooms. " +
                "Deactivate it instead.";

            return RedirectToAction(nameof(Index));
        }

        _context.ServicePrices.Remove(price);

        try
        {
            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Service price deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] =
                "Cannot delete this service price because " +
                "related records exist.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // VALIDATE SERVICE PRICE PERIOD
    // ==========================================

    private async Task ValidatePricePeriod(
        ServicePriceFormViewModel model,
        int? currentId = null)
    {
        // Date order is validated by the ViewModel.
        // Only active prices need overlap validation.
        if (!model.IsActive ||
            model.EffectiveFrom == default ||
            (model.EffectiveTo.HasValue &&
             model.EffectiveTo.Value < model.EffectiveFrom))
        {
            return;
        }

        // Check overlapping active periods
        // for the same service type.
        bool overlaps = await _context.ServicePrices
            .AsNoTracking()
            .AnyAsync(p =>
                p.ServiceType == model.ServiceType &&
                p.IsActive &&
                (!currentId.HasValue ||
                 p.Id != currentId.Value) &&

                // Existing period overlaps the new start.
                (!p.EffectiveTo.HasValue ||
                 p.EffectiveTo.Value >= model.EffectiveFrom) &&

                // New period overlaps the existing start.
                (!model.EffectiveTo.HasValue ||
                 p.EffectiveFrom <= model.EffectiveTo.Value)
            );

        if (overlaps)
        {
            ModelState.AddModelError(
                nameof(model.EffectiveFrom),
                "An active price for this service type " +
                "already exists during the selected period."
            );
        }
    }
}
