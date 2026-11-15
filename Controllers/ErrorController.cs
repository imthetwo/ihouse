
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ihouse.Controllers;

[AllowAnonymous]
public class ErrorController : Controller
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(
        ILogger<ErrorController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Index()
    {
        Response.StatusCode = 500;

        ViewBag.RequestId = HttpContext.TraceIdentifier;

        return View("Error");
    }
}
