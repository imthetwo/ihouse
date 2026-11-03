
using ihouse.Models;
using ihouse.Services;
using ihouse.ViewModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ihouse.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly EmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        EmailService email,
        IConfiguration config,
        ILogger<AccountController> logger)
    {
        _users = users;
        _signIn = signIn;
        _email = email;
        _config = config;
        _logger = logger;
    }

    // ================= LOGIN =================

    [HttpGet]
    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _users.FindByEmailAsync(model.Email);

        if (user == null)
        {
            ModelState.AddModelError(
                "",
                "Invalid email or password."
            );

            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true
        );

        if (result.Succeeded)
        {
            if (await _users.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("Index", "Admin");

            if (await _users.IsInRoleAsync(user, "Staff"))
                return RedirectToAction("Index", "Staff");

            if (await _users.IsInRoleAsync(user, "Tenant"))
                return RedirectToAction("Index", "TenantDashboard");

            await _signIn.SignOutAsync();

            ModelState.AddModelError(
                "",
                "Account role is not configured."
            );

            return View(model);
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(
                "",
                "Please verify your email before signing in."
            );
        }
        else if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                "",
                "Account temporarily locked. Try again later."
            );
        }
        else
        {
            ModelState.AddModelError(
                "",
                "Invalid email or password."
            );
        }

        return View(model);
    }

    // ================= REGISTER =================

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new ApplicationUser
        {
            FullName = model.FullName,
            UserName = model.Email,
            Email = model.Email
        };

        var result = await _users.CreateAsync(
            user,
            model.Password
        );

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    "",
                    error.Description
                );
            }

            return View(model);
        }

        // Public registration always creates a Tenant account.
        var roleResult = await _users.AddToRoleAsync(
            user,
            "Tenant"
        );

        if (!roleResult.Succeeded)
        {
            var rollback = await _users.DeleteAsync(user);

            if (!rollback.Succeeded)
            {
                throw new InvalidOperationException(
                    "Registration rollback failed."
                );
            }

            ModelState.AddModelError(
                "",
                "Unable to complete registration."
            );

            return View(model);
        }

        // Send verification email.
        try
        {
            await SendConfirmationEmailAsync(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send confirmation email for user {UserId}",
                user.Id
            );

            TempData["EmailNotice"] =
                "Your account was created, but the verification email could not be sent. Please request another email.";
        }

        return RedirectToAction(
            nameof(RegisterConfirmation)
        );
    }

    // ================= EMAIL CONFIRMATION =================

    private async Task SendConfirmationEmailAsync(
        ApplicationUser user)
    {
        var token =
            await _users.GenerateEmailConfirmationTokenAsync(user);

        var url = BuildAccountLink(
            nameof(ConfirmEmail),
            new
            {
                userId = user.Id,
                token
            }
        );

        await _email.SendEmailAsync(
            user.Email!,
            "iHouse - Verify Your Email",
            $"Please verify your email using this link:\n\n{url}"
        );
    }

    [HttpGet]
    public IActionResult RegisterConfirmation()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(
        string? userId,
        string? token)
    {
        ViewBag.Success = false;

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(token))
        {
            return View("EmailConfirmation");
        }

        var user = await _users.FindByIdAsync(userId);

        if (user == null)
        {
            return View("EmailConfirmation");
        }

        var result = await _users.ConfirmEmailAsync(
            user,
            token
        );

        ViewBag.Success = result.Succeeded;

        return View("EmailConfirmation");
    }

    // ================= RESEND CONFIRMATION EMAIL =================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendConfirmationEmail(
        string email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var user = await _users.FindByEmailAsync(email);

            if (user != null &&
                !await _users.IsEmailConfirmedAsync(user))
            {
                try
                {
                    await SendConfirmationEmailAsync(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to resend confirmation email for user {UserId}",
                        user.Id
                    );
                }
            }
        }

        // Do not reveal whether the email exists.
        TempData["EmailNotice"] =
            "If this email belongs to an unverified account, a verification email will be sent if delivery is available.";

        return RedirectToAction(
            nameof(RegisterConfirmation)
        );
    }

    // ================= FORGOT PASSWORD =================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _users.FindByEmailAsync(model.Email);

        if (user != null &&
            !string.IsNullOrEmpty(user.Email))
        {
            var token =
                await _users.GeneratePasswordResetTokenAsync(user);

            var url = BuildAccountLink(
                nameof(ResetPassword),
                new
                {
                    email = user.Email,
                    token
                }
            );

            try
            {
                await _email.SendEmailAsync(
                    user.Email,
                    "iHouse - Reset Password",
                    $"Open this link to reset your password:\n\n{url}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send password reset email."
                );
            }
        }

        return RedirectToAction(
            nameof(ForgotPasswordConfirmation)
        );
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View("ForgotPasswordConfirm");
    }

    // ================= RESET PASSWORD =================

    [HttpGet]
    public IActionResult ResetPassword(
        string? email,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("Invalid reset link.");
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _users.FindByEmailAsync(model.Email);

        if (user == null)
        {
            ModelState.AddModelError(
                "",
                "Invalid or expired reset link."
            );

            return View(model);
        }

        var result = await _users.ResetPasswordAsync(
            user,
            model.Token,
            model.NewPassword
        );

        if (result.Succeeded)
        {
            return RedirectToAction(
                nameof(ResetPasswordConfirmation)
            );
        }

        ModelState.AddModelError(
            "",
            "Unable to reset password. Check the link and password."
        );

        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View("ResetPasswordConfirm");
    }

    // ================= URL HELPER =================

    private string BuildAccountLink(
        string action,
        object routeValues)
    {
        var baseUrl = _config["App:BaseUrl"]
            ?? throw new InvalidOperationException(
                "App:BaseUrl is missing."
            );

        var origin = new Uri(
            baseUrl.TrimEnd('/') + "/"
        );

        if (origin.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Account links require HTTPS."
            );
        }

        var path = Url.Action(
            action,
            "Account",
            routeValues
        ) ?? throw new InvalidOperationException(
            "Cannot generate account link."
        );

        return new Uri(
            origin,
            path.TrimStart('/')
        ).ToString();
    }

    // ================= ACCESS DENIED =================

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ================= LOGOUT =================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();

        return RedirectToAction(nameof(Login));
    }
}
