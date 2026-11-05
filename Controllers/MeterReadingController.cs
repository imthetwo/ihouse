
using ihouse.Data;
using ihouse.Models;
using ihouse.ViewModels.MeterReading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class MeterReadingController : Controller
{
    private readonly ApplicationDbContext _context;

    public MeterReadingController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /MeterReading
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var readings = await _context.MeterReadings
            .AsNoTracking()
            .Include(r => r.Room)
            .OrderByDescending(r => r.ReadingDay)
            .ThenBy(r => r.Room.RoomNumber)
            .ToListAsync();

        return View(readings);
    }

    // GET: /MeterReading/Create
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadRoomsAsync();

        return View(new MeterReadingFormViewModel());
    }

    // POST: /MeterReading/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        MeterReadingFormViewModel model)
    {
        await ValidateReadingAsync(model);

        if (!ModelState.IsValid)
        {
            await LoadRoomsAsync();
            return View(model);
        }

        var reading = new MeterReading
        {
            RoomId = model.RoomId,
            ReadingDay = model.ReadingDay,
            ElectricityReading = model.ElectricityReading,
            WaterReading = model.WaterReading
        };

        _context.MeterReadings.Add(reading);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Meter reading created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /MeterReading/Edit/5
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var reading = await _context.MeterReadings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reading == null)
        {
            return NotFound();
        }

        var model = new MeterReadingFormViewModel
        {
            RoomId = reading.RoomId,
            ReadingDay = reading.ReadingDay,
            ElectricityReading = reading.ElectricityReading,
            WaterReading = reading.WaterReading
        };

        await LoadRoomsAsync();

        return View(model);
    }

    // POST: /MeterReading/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        MeterReadingFormViewModel model)
    {
        var reading = await _context.MeterReadings.FindAsync(id);

        if (reading == null)
        {
            return NotFound();
        }

        await ValidateReadingAsync(model, id);

        if (!ModelState.IsValid)
        {
            await LoadRoomsAsync();
            return View(model);
        }

        reading.RoomId = model.RoomId;
        reading.ReadingDay = model.ReadingDay;
        reading.ElectricityReading = model.ElectricityReading;
        reading.WaterReading = model.WaterReading;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Meter reading updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // POST: /MeterReading/Delete/5
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var reading = await _context.MeterReadings.FindAsync(id);

        if (reading == null)
        {
            return NotFound();
        }

        _context.MeterReadings.Remove(reading);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Meter reading deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // LOAD ROOM DROPDOWN
    private async Task LoadRoomsAsync()
    {
        ViewBag.RoomOptions = await _context.Rooms
            .AsNoTracking()
            .OrderBy(r => r.RoomNumber)
            .Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.RoomNumber
            })
            .ToListAsync();
    }

    // VALIDATE READING DATA
    private async Task ValidateReadingAsync(
        MeterReadingFormViewModel model,
        int? excludeId = null)
    {
        if (!await _context.Rooms.AnyAsync(r => r.Id == model.RoomId))
        {
            ModelState.AddModelError(
                nameof(model.RoomId),
                "Selected room does not exist.");

            return;
        }

        if (model.ReadingDay == default)
        {
            ModelState.AddModelError(
                nameof(model.ReadingDay),
                "Reading date is required.");

            return;
        }

        var sameDateExists = await _context.MeterReadings
            .AnyAsync(r =>
                r.RoomId == model.RoomId &&
                r.ReadingDay == model.ReadingDay &&
                (!excludeId.HasValue || r.Id != excludeId.Value));

        if (sameDateExists)
        {
            ModelState.AddModelError(
                nameof(model.ReadingDay),
                "A meter reading already exists for this room and date.");

            return;
        }

        var previousReading = await _context.MeterReadings
            .AsNoTracking()
            .Where(r =>
                r.RoomId == model.RoomId &&
                r.ReadingDay < model.ReadingDay &&
                (!excludeId.HasValue || r.Id != excludeId.Value))
            .OrderByDescending(r => r.ReadingDay)
            .FirstOrDefaultAsync();

        if (previousReading != null)
        {
            if (model.ElectricityReading <
                previousReading.ElectricityReading)
            {
                ModelState.AddModelError(
                    nameof(model.ElectricityReading),
                    "Electricity reading cannot be lower than the previous reading.");
            }

            if (model.WaterReading <
                previousReading.WaterReading)
            {
                ModelState.AddModelError(
                    nameof(model.WaterReading),
                    "Water reading cannot be lower than the previous reading.");
            }
        }

        var nextReading = await _context.MeterReadings
            .AsNoTracking()
            .Where(r =>
                r.RoomId == model.RoomId &&
                r.ReadingDay > model.ReadingDay &&
                (!excludeId.HasValue || r.Id != excludeId.Value))
            .OrderBy(r => r.ReadingDay)
            .FirstOrDefaultAsync();

        if (nextReading != null)
        {
            if (model.ElectricityReading >
                nextReading.ElectricityReading)
            {
                ModelState.AddModelError(
                    nameof(model.ElectricityReading),
                    "Electricity reading cannot exceed the next reading.");
            }

            if (model.WaterReading >
                nextReading.WaterReading)
            {
                ModelState.AddModelError(
                    nameof(model.WaterReading),
                    "Water reading cannot exceed the next reading.");
            }
        }
    }
}
