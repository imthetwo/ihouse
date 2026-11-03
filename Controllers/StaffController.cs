
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ihouse.Controllers;

[Authorize(Roles = "Staff")]
public class StaffController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
