
using ihouse.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ihouse.Controllers;

[Authorize(Roles = "Admin,Staff")]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        var roles = await _userManager.GetRolesAsync(user);

        ViewBag.Roles = string.Join(", ", roles);

        return View(user);
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword(
        string? currentPassword, string? newPassword, string? confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
            ModelState.AddModelError("currentPassword", "Enter your current password.");
        if (string.IsNullOrWhiteSpace(newPassword))
            ModelState.AddModelError("newPassword", "Enter a new password.");
        else if (newPassword.Length > 128)
            ModelState.AddModelError("newPassword", "Use no more than 128 characters.");
        if (string.IsNullOrEmpty(confirmPassword))
            ModelState.AddModelError("confirmPassword", "Confirm your new password.");
        else if (newPassword != confirmPassword)
            ModelState.AddModelError("confirmPassword", "Passwords do not match.");
        if (!ModelState.IsValid) return View();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var result = await _userManager.ChangePasswordAsync(user, currentPassword!, newPassword!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                string message = error.Code switch
                {
                    "PasswordMismatch" => "Your current password is incorrect.",
                    "PasswordTooShort" => $"Use at least {_userManager.Options.Password.RequiredLength} characters.",
                    "PasswordRequiresDigit" => "Include a number in your new password.",
                    "PasswordRequiresUpper" => "Include an uppercase letter in your new password.",
                    "PasswordRequiresLower" => "Include a lowercase letter in your new password.",
                    "PasswordRequiresNonAlphanumeric" => "Include a symbol in your new password.",
                    "PasswordRequiresUniqueChars" => "Use more different characters in your new password.",
                    _ => "We couldn't change your password. Please try again."
                };
                ModelState.AddModelError(error.Code == "PasswordMismatch" ? "currentPassword" : "newPassword", message);
            }
            _logger.LogWarning("Password change failed for {UserId}: {Codes}", user.Id,
                string.Join(", ", result.Errors.Select(e => e.Code)));
            return View();
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(Index));
    }
}
