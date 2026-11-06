
using ihouse.Data;
using ihouse.Models;
using ihouse.ViewModels.Room;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class RoomsController : Controller
{
    private readonly ApplicationDbContext _context;

    public RoomsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // INDEX - ADMIN & STAFF
    // ==========================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var rooms = await _context.Rooms
            .AsNoTracking()
            .OrderBy(r => r.RoomNumber)
            .ToListAsync();

        return View(rooms);
    }

    // ==========================================
    // CREATE - ADMIN ONLY
    // ==========================================

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new RoomFormViewModel());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        RoomFormViewModel model)
    {
        ValidateInput(model);

        if (!ModelState.IsValid)
            return View(model);

        string roomNumber = model.RoomNumber.Trim();

        bool exists = await _context.Rooms
            .AnyAsync(r =>
                r.RoomNumber.ToLower() ==
                roomNumber.ToLower());

        if (exists)
        {
            ModelState.AddModelError(
                nameof(model.RoomNumber),
                "Room number already exists.");

            return View(model);
        }

        var room = new Room
        {
            RoomNumber = roomNumber,
            Floor = model.Floor,
            RoomType = model.RoomType.Trim(),
            Capacity = model.Capacity,
            RentPrice = model.RentPrice,
            Status = model.Status,
            Description = model.Description?.Trim(),
            ImageUrl = model.ImageUrl?.Trim()
        };

        _context.Rooms.Add(room);

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Room created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // EDIT - ADMIN ONLY
    // ==========================================

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null)
            return NotFound();

        var model = new RoomFormViewModel
        {
            RoomNumber = room.RoomNumber,
            Floor = room.Floor,
            RoomType = room.RoomType,
            Capacity = room.Capacity,
            RentPrice = room.RentPrice,
            Status = room.Status,
            Description = room.Description,
            ImageUrl = room.ImageUrl
        };

        return View(model);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        RoomFormViewModel model)
    {
        ValidateInput(model);

        if (!ModelState.IsValid)
            return View(model);

        var room = await _context.Rooms
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null)
            return NotFound();

        string roomNumber = model.RoomNumber.Trim();

        bool exists = await _context.Rooms
            .AnyAsync(r =>
                r.Id != id &&
                r.RoomNumber.ToLower() ==
                roomNumber.ToLower());

        if (exists)
        {
            ModelState.AddModelError(
                nameof(model.RoomNumber),
                "Room number already exists.");

            return View(model);
        }

        room.RoomNumber = roomNumber;
        room.Floor = model.Floor;
        room.RoomType = model.RoomType.Trim();
        room.Capacity = model.Capacity;
        room.RentPrice = model.RentPrice;
        room.Status = model.Status;
        room.Description = model.Description?.Trim();
        room.ImageUrl = model.ImageUrl?.Trim();

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Room updated successfully.";

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
        var room = await _context.Rooms
            .FirstOrDefaultAsync(r => r.Id == id);

        if (room == null)
            return NotFound();

        // Protect related rental data.
        bool hasRelatedData =
            await _context.Contracts
                .AnyAsync(c => c.RoomId == id)
            ||
            await _context.RoomServices
                .AnyAsync(s => s.RoomId == id)
            ||
            await _context.MeterReadings
                .AnyAsync(m => m.RoomId == id)
            ||
            await _context.MaintenanceRequests
                .AnyAsync(m => m.RoomId == id);

        if (hasRelatedData)
        {
            TempData["Error"] =
                "Cannot delete a room with related records.";

            return RedirectToAction(nameof(Index));
        }

        _context.Rooms.Remove(room);

        try
        {
            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Room deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] =
                "Cannot delete this room because related records exist.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // VALIDATION
    // ==========================================

    private void ValidateInput(RoomFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.RoomNumber))
        {
            ModelState.AddModelError(
                nameof(model.RoomNumber),
                "Room number is required.");
        }

        if (string.IsNullOrWhiteSpace(model.RoomType))
        {
            ModelState.AddModelError(
                nameof(model.RoomType),
                "Room type is required.");
        }
    }
}
