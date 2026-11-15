
using ihouse.Models;
using ihouse.ViewModels.UserManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin")]
public class UserManagementController : Controller
{
	private readonly UserManager<ApplicationUser> _userManager;
	private readonly RoleManager<IdentityRole> _roleManager;
	private readonly ILogger<UserManagementController> _logger;

	private static readonly string[] AllowedRoles =
	{
		"Admin",
		"Staff",
		"Tenant"
	};

	public UserManagementController(
		UserManager<ApplicationUser> userManager,
		RoleManager<IdentityRole> roleManager,
		ILogger<UserManagementController> logger)
	{
		_userManager = userManager;
		_roleManager = roleManager;
		_logger = logger;
	}

	// GET: /UserManagement
	[HttpGet]
	public async Task<IActionResult> Index()
	{
		var users = await _userManager.Users
			.AsNoTracking()
			.OrderBy(u => u.Email)
			.ToListAsync();

		var model = new List<UserListItemViewModel>();

		foreach (var user in users)
		{
			var roles = await _userManager.GetRolesAsync(user);

			model.Add(new UserListItemViewModel
			{
				Id = user.Id,
				FullName = user.FullName ?? "",
				Email = user.Email ?? "",
				Role = roles.Count == 1
					? roles[0]
					: roles.Count == 0
						? "Unassigned"
						: "Multiple Roles",
				EmailConfirmed = user.EmailConfirmed
			});
		}

		return View(model);
	}

	// GET: /UserManagement/EditRole/id
	[HttpGet]
	public async Task<IActionResult> EditRole(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
			return NotFound();

		var user = await _userManager.FindByIdAsync(id);

		if (user == null)
			return NotFound();

		var roles = await _userManager.GetRolesAsync(user);

		if (roles.Count > 1)
		{
			TempData["Error"] =
				"This account has multiple roles. Review it before editing.";

			return RedirectToAction(nameof(Index));
		}

		var currentRole = roles.FirstOrDefault() ?? "";

		var model = new EditUserRoleViewModel
		{
			UserId = user.Id,
			FullName = user.FullName ?? "",
			Email = user.Email ?? "",
			CurrentRole = currentRole,
			NewRole = AllowedRoles.Contains(currentRole)
				? currentRole
				: ""
		};

		return View(model);
	}

	// POST: /UserManagement/EditRole
	[HttpPost]
	[ValidateAntiForgeryToken]
	public async Task<IActionResult> EditRole(
		EditUserRoleViewModel model)
	{
		if (!AllowedRoles.Contains(model.NewRole))
		{
			ModelState.AddModelError(
				nameof(model.NewRole),
				"Invalid role selected.");
		}

		if (!ModelState.IsValid)
			return View(model);

		var targetUser =
			await _userManager.FindByIdAsync(model.UserId);

		if (targetUser == null)
			return NotFound();

		// Prevent changing your own privileges.
		var adminId = _userManager.GetUserId(User);

		if (string.IsNullOrEmpty(adminId))
			return Challenge();

		if (targetUser.Id == adminId)
		{
			TempData["Error"] =
				"You cannot change your own role.";

			return RedirectToAction(nameof(Index));
		}

		// Re-authenticate the administrator.
		var currentAdmin =
			await _userManager.FindByIdAsync(adminId);

		if (currentAdmin == null)
			return Challenge();

		var validPassword = await _userManager.CheckPasswordAsync(
			currentAdmin,
			model.AdminPassword);

		if (!validPassword)
		{
			ModelState.AddModelError(
				nameof(model.AdminPassword),
				"Incorrect administrator password.");

			return View(model);
		}

		var currentRoles =
			await _userManager.GetRolesAsync(targetUser);

		if (currentRoles.Count > 1)
		{
			TempData["Error"] =
				"Cannot change an account with multiple roles.";

			return RedirectToAction(nameof(Index));
		}

		var oldRole = currentRoles.FirstOrDefault();

		if (oldRole != null && !AllowedRoles.Contains(oldRole))
		{
			TempData["Error"] =
				"This account cannot be updated right now. Please try again later.";

			return RedirectToAction(nameof(Index));
		}

		if (oldRole == model.NewRole)
		{
			TempData["Success"] =
				"This account already has the selected role.";

			return RedirectToAction(nameof(Index));
		}

		if ((model.NewRole == "Admin" ||
			 model.NewRole == "Staff") &&
			!targetUser.EmailConfirmed)
		{
			ModelState.AddModelError(
				nameof(model.NewRole),
				"Email must be verified before granting staff or admin access.");

			return View(model);
		}

		if (!await _roleManager.RoleExistsAsync(model.NewRole))
		{
			TempData["Error"] =
				"This role is currently unavailable. Please choose another role.";

			return RedirectToAction(nameof(Index));
		}

		// Add the new role first.
		var addResult = await _userManager.AddToRoleAsync(
			targetUser,
			model.NewRole);

		if (!addResult.Succeeded)
		{
			TempData["Error"] =
				"Unable to assign the selected role.";

			return RedirectToAction(nameof(Index));
		}

		// Remove the previous role.
		if (!string.IsNullOrEmpty(oldRole))
		{
			var removeResult = await _userManager.RemoveFromRoleAsync(
				targetUser,
				oldRole);

			if (!removeResult.Succeeded)
			{
				// Try to restore the previous role state.
				var rollback = await _userManager.RemoveFromRoleAsync(
					targetUser,
					model.NewRole);

				_logger.LogError("Role removal failed for {UserId}. Rollback succeeded: {Rollback}", targetUser.Id, rollback.Succeeded);
				TempData["Error"] = rollback.Succeeded
					? "We couldn't change the role. Please try again."
					: "We couldn't finish changing the role. Please check the account before trying again.";

				return RedirectToAction(nameof(Index));
			}
		}

		// Invalidate existing sign-in cookies.
		var stampResult =
			await _userManager.UpdateSecurityStampAsync(targetUser);

		if (!stampResult.Succeeded)
			_logger.LogError("Security stamp update failed for {UserId}: {Errors}", targetUser.Id, stampResult.Errors);

		TempData[stampResult.Succeeded ? "Success" : "Error"] =
			stampResult.Succeeded
				? "User role updated successfully."
				: "The role was changed, but we couldn't finish securing the account. Please try again later.";

		return RedirectToAction(nameof(Index));
	}
}
