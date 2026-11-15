using Microsoft.AspNetCore.Mvc.Razor;

namespace ihouse.Configuration;

public static class ViewFolders
{
    public static void Configure(RazorViewEngineOptions options)
    {
        foreach (var folder in new[] { "Accounts", "Dashboards", "Rentals", "Billing" })
            options.ViewLocationFormats.Insert(0, $"/Views/{folder}/{{1}}/{{0}}.cshtml");
    }
}
