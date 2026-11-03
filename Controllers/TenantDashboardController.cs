using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ihouse.Controllers;

[Authorize(Roles = "Tenant")]
public class TenantDashboardController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return Content("Welcome to Tenant Dashboard!");
    }
}