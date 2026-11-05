
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

    // GET: /ServicePrice
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

    // GET: /ServicePrice/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new ServicePriceFormViewModel());
    }

    // POST: /ServicePrice/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ServicePriceFormViewModel model)
    {
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

        TempData["Success"] = "Service price created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /ServicePrice/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var price = await _context.ServicePrices.FindAsync(id);

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

    // POST: /ServicePrice/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ServicePriceFormViewModel model)
    {
        var price = await _context.ServicePrices.FindAsync(id);

        if (price == null)
        {
            return NotFound();
        }

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

        TempData["Success"] = "Service price updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /ServicePrice/Delete/5
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var price = await _context.ServicePrices
            .Include(p => p.RoomServices)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (price == null)
        {
            return NotFound();
        }

        if (price.RoomServices.Any())
        {
            TempData["Error"] =
                "Cannot delete a service price linked to rooms.";

            return RedirectToAction(nameof(Index));
        }

        _context.ServicePrices.Remove(price);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Service price deleted successfully.";

        return RedirectToAction(nameof(Index));
    }
}
