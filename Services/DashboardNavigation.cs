using System.Security.Claims;

namespace ihouse.Services;

public static class DashboardNavigation
{
    public static string ControllerFor(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return "Account";
        if (user.IsInRole("Admin")) return "Admin";
        if (user.IsInRole("Staff")) return "Staff";
        if (user.IsInRole("Tenant")) return "TenantDashboard";
        return "Account";
    }

    public static string ActionFor(ClaimsPrincipal user)
        => ControllerFor(user) == "Account" ? "Login" : "Index";
}
