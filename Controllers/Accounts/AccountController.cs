
using ihouse.Models;
using ihouse.Services;
using ihouse.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
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

    // ==========================================
    // LOGIN - GET
    // ==========================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        var controller = DashboardNavigation.ControllerFor(User);
        if (controller != "Account")
            return RedirectToAction("Index", controller);

        return View(new LoginViewModel());
    }

    // ==========================================
    // LOGIN - POST
    // ==========================================

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(
        LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(
            model.Email.Trim());

        if (user == null)
        {
            ModelState.AddModelError(
                "",
                "Invalid email or password.");

            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(
            user,
            model.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (await _users.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }

            if (await _users.IsInRoleAsync(user, "Staff"))
            {
                return RedirectToAction("Index", "Staff");
            }

            if (await _users.IsInRoleAsync(user, "Tenant"))
            {
                return RedirectToAction(
                    "Index",
                    "TenantDashboard");
            }

            await _signIn.SignOutAsync();

            ModelState.AddModelError(
                "",
                "You cannot sign in right now. Please contact the property manager.");

            return View(model);
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(
                "",
                "Please verify your email before signing in.");
        }
        else if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                "",
                "Account temporarily locked. Try again later.");
        }
        else
        {
            ModelState.AddModelError(
                "",
                "Invalid email or password.");
        }

        return View(model);
    }

    // ==========================================
    // REGISTER - GET
    // ==========================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    // ==========================================
    // REGISTER - POST
    // ==========================================

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(
        RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();

        var existingUser =
            await _users.FindByEmailAsync(email);

        // Existing account
        if (existingUser != null)
        {
            // Email has not been confirmed.
            if (!await _users.IsEmailConfirmedAsync(existingUser))
            {
                try
                {
                    await SendConfirmationEmailAsync(
                        existingUser);

                    TempData["EmailNotice"] =
                        "Please check your inbox and spam folder " +
                        "for an email verification link. " +
                        "Your account must be verified before signing in.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to resend verification email for user {UserId}",
                        existingUser.Id);

                    TempData["EmailNotice"] =
                        "Your email is awaiting verification. " +
                        "If the message has not arrived, " +
                        "please use Resend Verification Email.";
                }

                // Do not overwrite existing credentials.
                return RedirectToAction(
                    nameof(RegisterConfirmation));
            }

            // Email already confirmed.
            ModelState.AddModelError(
                nameof(model.Email),
                "This email is already registered. " +
                "Please sign in instead.");

            return View(model);
        }

        // New account
        var user = new ApplicationUser
        {
            FullName = model.FullName.Trim(),
            UserName = email,
            Email = email,
            EmailConfirmed = false
        };

        var result = await _users.CreateAsync(
            user,
            model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var message = error.Code switch
                {
                    "DuplicateEmail" or "DuplicateUserName" => "This email is already registered. Please sign in instead.",
                    "InvalidEmail" or "InvalidUserName" => "Enter a valid email address.",
                    "PasswordTooShort" => "Use at least 8 characters for your password.",
                    "PasswordRequiresDigit" => "Include a number in your password.",
                    "PasswordRequiresUpper" => "Include an uppercase letter in your password.",
                    "PasswordRequiresLower" => "Include a lowercase letter in your password.",
                    _ => "We couldn't create your account. Please try again later."
                };
                ModelState.AddModelError("", message);
            }

            return View(model);
        }

        // Public registration creates Tenant account.
        var roleResult = await _users.AddToRoleAsync(
            user,
            "Tenant");

        if (!roleResult.Succeeded)
        {
            var rollback =
                await _users.DeleteAsync(user);

            if (!rollback.Succeeded)
            {
                _logger.LogError(
                    "Failed to roll back registration for user {UserId}",
                    user.Id);

                throw new InvalidOperationException(
                    "Registration rollback failed.");
            }

            ModelState.AddModelError(
                "",
                "Unable to complete registration.");

            return View(model);
        }

        try
        {
            await SendConfirmationEmailAsync(user);

            TempData["EmailNotice"] =
                "Please check your email for a verification link. " +
                "Please check your inbox and spam folder. " +
                "You cannot sign in until your email is verified.";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send verification email for user {UserId}",
                user.Id);

            TempData["EmailNotice"] =
                "Your account was created but the verification " +
                "email could not be sent. " +
                "Please use Resend Verification Email.";
        }

        return RedirectToAction(
            nameof(RegisterConfirmation));
    }

    // ==========================================
    // SEND VERIFICATION EMAIL
    // ==========================================

    private async Task SendConfirmationEmailAsync(
        ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException(
                "User email is missing.");
        }

        if (await _users.IsEmailConfirmedAsync(user))
        {
            return;
        }

        var token =
            await _users.GenerateEmailConfirmationTokenAsync(user);

        var url = BuildAccountLink(
            nameof(ConfirmEmail),
            new
            {
                userId = user.Id,
                token
            });

        await _email.SendEmailAsync(
            user.Email,
            "iHouse - Verify Your Email",
            $"Welcome to iHouse!\n\n" +
            $"Please verify your email address " +
            $"by opening the link below:\n\n" +
            $"{url}\n\n" +
            $"The verification link is valid for 24 hours.\n\n" +
            $"If you did not create this account, " +
            $"you can ignore this email.");
    }

    // ==========================================
    // REGISTER CONFIRMATION
    // ==========================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult RegisterConfirmation()
    {
        return View();
    }

    // ==========================================
    // CONFIRM EMAIL
    // ==========================================

    [AllowAnonymous]
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

        if (await _users.IsEmailConfirmedAsync(user))
        {
            ViewBag.Success = true;

            return View("EmailConfirmation");
        }

        var result = await _users.ConfirmEmailAsync(
            user,
            token);

        ViewBag.Success = result.Succeeded;

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "Email verification failed for user {UserId}",
                user.Id);
        }

        return View("EmailConfirmation");
    }

    // ==========================================
    // RESEND VERIFICATION EMAIL
    // ==========================================

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendConfirmationEmail(
        string email)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            var user = await _users.FindByEmailAsync(
                email.Trim());

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
                        user.Id);
                }
            }
        }

        TempData["EmailNotice"] =
            "If your account needs verification, check your inbox and spam folder for a verification link.";

        return RedirectToAction(
            nameof(RegisterConfirmation));
    }

    // ==========================================
    // FORGOT PASSWORD - GET
    // ==========================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    // ==========================================
    // FORGOT PASSWORD - POST
    // ==========================================

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(
            model.Email.Trim());

        if (user != null &&
            !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                var token =
                    await _users.GeneratePasswordResetTokenAsync(user);

                var url = BuildAccountLink(
                    nameof(ResetPassword),
                    new
                    {
                        email = user.Email,
                        token
                    });

                await _email.SendEmailAsync(
                    user.Email,
                    "iHouse - Reset Password",
                    $"Open this link to reset your password:\n\n{url}");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send password reset email.");
            }
        }

        return RedirectToAction(
            nameof(ForgotPasswordConfirmation));
    }

    // ==========================================
    // FORGOT PASSWORD CONFIRMATION
    // ==========================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View("ForgotPasswordConfirm");
    }

    // ==========================================
    // RESET PASSWORD - GET
    // ==========================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(
        string? email,
        string? token)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(token))
        {
            Response.StatusCode = 400;
            ViewBag.ErrorTitle = "Reset link unavailable";
            ViewBag.ErrorMessage = "This password reset link is invalid. Please request a new link.";
            return View("~/Views/Shared/Error.cshtml");
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        });
    }

    // ==========================================
    // RESET PASSWORD - POST
    // ==========================================

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _users.FindByEmailAsync(
            model.Email);

        if (user == null)
        {
            ModelState.AddModelError(
                "",
                "Invalid or expired reset link.");

            return View(model);
        }

        var result = await _users.ResetPasswordAsync(
            user,
            model.Token,
            model.NewPassword);

        if (result.Succeeded)
        {
            return RedirectToAction(
                nameof(ResetPasswordConfirmation));
        }

        ModelState.AddModelError(
            "",
            "Unable to reset password. " +
            "Check the link and password.");

        return View(model);
    }

    // ==========================================
    // RESET PASSWORD CONFIRMATION
    // ==========================================

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View("ResetPasswordConfirm");
    }

    // ==========================================
    // ACCOUNT LINK HELPER
    // ==========================================

    private string BuildAccountLink(
        string action,
        object routeValues)
    {
        var baseUrl = _config["App:BaseUrl"]
            ?? throw new InvalidOperationException(
                "App:BaseUrl is missing.");

        var origin = new Uri(
            baseUrl.TrimEnd('/') + "/");

        if (origin.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Account links require HTTPS.");
        }

        var path = Url.Action(
            action,
            "Account",
            routeValues)
            ?? throw new InvalidOperationException(
                "Cannot generate account link.");

        return new Uri(
            origin,
            path.TrimStart('/')).ToString();
    }

    // ==========================================
    // ACCESS DENIED
    // ==========================================

    [HttpGet]
    public IActionResult AccessDenied()
    { 
        return View();
    }

    // ==========================================
    // LOGOUT
    // ==========================================

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();

        return RedirectToAction(nameof(Login));
    }
}
