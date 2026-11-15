
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ihouse.Controllers;

[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(
        ILogger<ErrorController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        Response.StatusCode = 500;

        var error = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (error != null)
            _logger.LogError(error.Error, "Request failed at {Path}", error.Path);

        return View("Error");
    }

    public IActionResult Status(int code)
    {
        Response.StatusCode = code >= 400 && code <= 599 ? code : 500;
        ViewBag.ErrorTitle = code == 404 ? "Page not found" : "Unable to complete your request";
        ViewBag.ErrorMessage = code == 404
            ? "This page is no longer available. Please return to the home page."
            : "Please try again. If the problem continues, contact your property manager.";
        return View("Error");
    }
}
