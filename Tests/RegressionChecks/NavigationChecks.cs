using System.Reflection;
using System.Security.Claims;
using ihouse.Controllers;
using ihouse.Models;
using ihouse.Services;
using ihouse.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

static class NavigationChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var config = new ConfigurationBuilder().Build();
        foreach (var (role, destination) in new[]
        {
            ("Admin", "Admin"), ("Staff", "Staff"), ("Tenant", "TenantDashboard")
        })
        {
            var user = Principal(role);
            check(DashboardNavigation.ControllerFor(user) == destination &&
                DashboardNavigation.ActionFor(user) == "Index", role + " dashboard destination");

            using var users = new TestUsers(role);
            var signIn = new TestSignIn(users);
            var controller = new AccountController(users, signIn, new EmailService(config), config,
                NullLogger<AccountController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } }
            };
            var get = controller.Login() as RedirectToActionResult;
            check(get?.ControllerName == destination && get.ActionName == "Index",
                role + " visiting Login returns to own dashboard");
            var post = await controller.Login(new LoginViewModel
            {
                Email = "test@example.test", Password = "Example123"
            }) as RedirectToActionResult;
            check(post?.ControllerName == destination && post.ActionName == "Index",
                role + " successful sign-in redirects to own dashboard (fake Identity)");
            var logout = await controller.Logout() as RedirectToActionResult;
            check(signIn.SignedOut && logout?.ActionName == "Login",
                role + " logout signs out and returns to Login (fake Identity)");
        }
        check(DashboardNavigation.ControllerFor(new ClaimsPrincipal(new ClaimsIdentity())) == "Account" &&
            DashboardNavigation.ActionFor(new ClaimsPrincipal(new ClaimsIdentity())) == "Login",
            "Anonymous home destination is Login");
        check(DashboardNavigation.ControllerFor(Principal("Unknown")) == "Account",
            "Unrecognized roles do not receive a privileged dashboard");

        using (var users = new TestUsers("Staff"))
        {
            var signIn = new TestSignIn(users);
            ProfileController Profile() => new(users, signIn, NullLogger<ProfileController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Principal("Staff") } },
                TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
            };
            var invalid = Profile();
            check(await invalid.ChangePassword("old", "NewPassword123", "different") is ViewResult && users.PasswordChanges == 0,
                "Password confirmation mismatch never calls Identity password change");
            users.PasswordResult = IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch", Description = "internal detail" });
            var incorrect = Profile();
            check(await incorrect.ChangePassword("wrong", "NewPassword123", "NewPassword123") is ViewResult &&
                incorrect.ModelState["currentPassword"]!.Errors[0].ErrorMessage == "Your current password is incorrect." && !signIn.Refreshed,
                "Incorrect current password displays a safe message without refreshing the session");
            users.PasswordResult = IdentityResult.Success;
            var success = Profile();
            var changed = await success.ChangePassword("old", "NewPassword123", "NewPassword123") as RedirectToActionResult;
            check(changed?.ActionName == "Index" && signIn.Refreshed && success.TempData["Success"] != null,
                "Successful password change refreshes sign-in and confirms success (fake Identity)");
        }

        using var provider = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var allowed = new Dictionary<Type, string[]>
        {
            [typeof(AdminController)] = ["Admin"],
            [typeof(StaffController)] = ["Staff"],
            [typeof(UserManagementController)] = ["Admin"],
            [typeof(TenantDashboardController)] = ["Tenant"],
            [typeof(TenantPaymentsController)] = ["Tenant"],
            [typeof(PayPalController)] = ["Tenant"],
            [typeof(RoomsController)] = ["Admin", "Staff"],
            [typeof(TenantsController)] = ["Admin", "Staff"],
            [typeof(ContractsController)] = ["Admin", "Staff"],
            [typeof(InvoicesController)] = ["Admin", "Staff"],
            [typeof(PaymentController)] = ["Admin", "Staff"],
            [typeof(MeterReadingController)] = ["Admin", "Staff"],
            [typeof(ServicePriceController)] = ["Admin", "Staff"],
            [typeof(MaintenanceController)] = ["Admin", "Staff"],
            [typeof(ProfileController)] = ["Admin", "Staff"]
        };
        foreach (var (controller, roles) in allowed)
        {
            var policy = await AuthorizationPolicy.CombineAsync(policies,
                controller.GetCustomAttributes<AuthorizeAttribute>())
                ?? throw new Exception("Missing authorization on " + controller.Name);
            foreach (var role in new[] { "Admin", "Staff", "Tenant", "Anonymous" })
            {
                var result = await authorization.AuthorizeAsync(Principal(role), null, policy);
                check(result.Succeeded == roles.Contains(role), controller.Name + " access for " + role);
            }
        }
        // These restrictions must remain in effect for both GET and POST overloads.
        foreach (var (type, action) in new (Type, string)[]
        {
            (typeof(RoomsController), "Create"), (typeof(RoomsController), "Edit"), (typeof(RoomsController), "Delete"),
            (typeof(TenantsController), "Delete"), (typeof(ContractsController), "Terminate"),
            (typeof(InvoicesController), "Edit"), (typeof(InvoicesController), "DeleteConfirmed"),
            (typeof(MeterReadingController), "Edit"), (typeof(MeterReadingController), "Delete"),
            (typeof(ServicePriceController), "Create"), (typeof(ServicePriceController), "Edit"),
            (typeof(ServicePriceController), "Delete")
        })
        {
            foreach (var method in type.GetMethods().Where(m => m.Name == action))
            {
                var policy = await AuthorizationPolicy.CombineAsync(policies,
                    type.GetCustomAttributes<AuthorizeAttribute>().Concat(method.GetCustomAttributes<AuthorizeAttribute>()))
                    ?? throw new Exception("Missing action authorization");
                check(!(await authorization.AuthorizeAsync(Principal("Staff"), null, policy)).Succeeded &&
                    (await authorization.AuthorizeAsync(Principal("Admin"), null, policy)).Succeeded,
                    type.Name + "." + action + " stays Admin-only");
            }
        }
        var logoutMethod = typeof(AccountController).GetMethod(nameof(AccountController.Logout))!;
        check(logoutMethod.IsDefined(typeof(HttpPostAttribute)) && logoutMethod.IsDefined(typeof(ValidateAntiForgeryTokenAttribute)),
            "Logout remains POST-only with antiforgery validation");

        // Render the actual compiled Razor views without starting the app or accessing its database.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AccountController).Assembly.GetName().Name,
            ContentRootPath = Directory.GetCurrentDirectory(), EnvironmentName = "Production"
        });
        builder.Services.AddControllersWithViews()
            .AddRazorOptions(ihouse.Configuration.ViewFolders.Configure)
            .AddApplicationPart(typeof(AccountController).Assembly);
        builder.Services.AddSingleton<UserManager<ApplicationUser>>(new TestUsers("Staff"));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        await using var app = builder.Build();
        app.UseRouting();
        app.MapControllerRoute("default", "{controller=Account}/{action=Login}/{id?}");
        // Register route data for link generation without starting a web server.
#pragma warning disable ASP0014
        app.UseEndpoints(_ => { });
#pragma warning restore ASP0014
        using (var scope = app.Services.CreateScope())
        {
            var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
            foreach (var group in new[] { "Accounts", "Dashboards", "Rentals", "Billing" })
            {
                foreach (var folder in Directory.GetDirectories(Path.Combine("Views", group)))
                {
                    foreach (var file in Directory.GetFiles(folder, "*.cshtml"))
                    {
                        var name = Path.GetFileNameWithoutExtension(file);
                        var route = new RouteData();
                        route.Values["controller"] = Path.GetFileName(folder);
                        route.Values["action"] = name;
                        var action = new ActionContext(new DefaultHttpContext { RequestServices = scope.ServiceProvider },
                            route, new ActionDescriptor());
                        var result = engine.FindView(action, name, !name.StartsWith('_'));
                        check(result.Success && result.View.Path == "/" + file.Replace('\\', '/'),
                            "Conventional View() resolves " + file);
                    }
                }
            }
        }
        foreach (var (role, destination) in new[] { ("Admin", "Admin"), ("Staff", "Staff"), ("Tenant", "TenantDashboard") })
        {
            var denied = await RenderAsync(app.Services, "/Views/Accounts/Account/AccessDenied.cshtml", role, null);
            check(denied.Contains($"href=\"/{destination}/Index\""), role + " Access Denied home link renders correctly");
            var error = await RenderAsync(app.Services, "/Views/Shared/Error.cshtml", role, null);
            check(error.Contains($"href=\"/{destination}/Index\""), role + " error page home link renders correctly");
        }
        foreach (var role in new[] { "Admin", "Staff" })
        {
            var html = await RenderAsync(app.Services, "/Views/Dashboards/Staff/Index.cshtml", role,
                new ihouse.ViewModels.Staff.StaffDashboardViewModel());
            check(html.Split($"href=\"/{role}/Index\"").Length - 1 == 3,
                role + " logo, sidebar and account dashboard links render correctly");
            check(html.Contains("/UserManagement/Index") == (role == "Admin"),
                role + " user management menu matches permissions");
            check(html.Contains("/Profile/Index") && html.Contains("/Profile/ChangePassword") &&
                html.Contains("Test User") && html.Contains("test@example.test"),
                role + " account menu shows profile, password change, name and email");
            if (role == "Staff") check(!html.Contains("href=\"/Admin/Index\""), "Staff layout never links to Admin dashboard");
        }
        var contracts = new[]
        {
            new Contract { Id = 1, Status = ihouse.Models.Enums.ContractStatus.Active, Room = new Room { RoomNumber = "101" } },
            new Contract { Id = 2, Status = ihouse.Models.Enums.ContractStatus.Pending, Room = new Room { RoomNumber = "102" } }
        };
        var staffContracts = await RenderAsync(app.Services, "/Views/Rentals/Contracts/Index.cshtml", "Staff", contracts);
        check(!staffContracts.Contains("/Contracts/Terminate") && !staffContracts.Contains("/Contracts/Edit/1") &&
            staffContracts.Contains("/Contracts/Edit/2"), "Staff contract buttons allow Pending edit and hide restricted actions");
        var adminContracts = await RenderAsync(app.Services, "/Views/Rentals/Contracts/Index.cshtml", "Admin", contracts);
        check(adminContracts.Contains("/Contracts/Terminate/1") && adminContracts.Contains("/Contracts/Edit/1"),
            "Admin retains contract edit and termination buttons");
        var connect = await RenderAsync(app.Services, "/Views/Dashboards/TenantDashboard/Connect.cshtml", "Tenant", null);
        check(System.Text.RegularExpressions.Regex.Matches(connect, "id=\"connectContractForm\"").Count == 1 &&
            System.Text.RegularExpressions.Regex.Matches(connect, "id=\"code\"").Count == 1,
            "Connect Contract renders one form and one invitation input");
        check(connect.Contains("/css/tenant-connect.css") && connect.Contains("/js/tenant-connect.js"),
            "Connect Contract loads its separate CSS and JavaScript");
        foreach (var (path, role, model) in new (string, string, object?)[]
        {
            ("/Views/Dashboards/Staff/Index.cshtml", "Staff", new ihouse.ViewModels.Staff.StaffDashboardViewModel()),
            ("/Views/Dashboards/TenantDashboard/Connect.cshtml", "Tenant", null),
            ("/Views/Accounts/Account/Login.cshtml", "Anonymous", new LoginViewModel()),
            ("/Views/Accounts/Account/RegisterConfirmation.cshtml", "Anonymous", null),
            ("/Views/Shared/Error.cshtml", "Anonymous", null),
            ("/Views/Accounts/Account/Error.cshtml", "Anonymous", null)
        })
        {
            var html = await RenderAsync(app.Services, path, role, model);
            check(System.Text.RegularExpressions.Regex.Matches(html, "class=\"ihouse-logo\"").Count == 1 &&
                html.Contains("/css/brand.css") && html.Contains("ihouse-logo-icon") && html.Contains("ihouse-logo-name"),
                path + " renders the shared iHouse logo and stylesheet");
        }
    }

    private static async Task<string> RenderAsync(IServiceProvider services, string path, string role, object? model)
    {
        using var scope = services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = Principal(role) };
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("localhost");
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Razor test"));
        var route = new RouteData();
        route.Values["controller"] = Path.GetFileName(Path.GetDirectoryName(path));
        route.Values["action"] = "Index";
        var action = new ActionContext(http, route, new ActionDescriptor());
        var view = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null, path, true);
        if (!view.Success) throw new Exception("View not found: " + path);
        using var writer = new StringWriter();
        await view.View.RenderAsync(new ViewContext(action, view.View,
            new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model },
            new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions()));
        return writer.ToString();
    }

    private static ClaimsPrincipal Principal(string role) => role == "Anonymous"
        ? new ClaimsPrincipal(new ClaimsIdentity())
        : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test"),
            new Claim(ClaimTypes.Role, role)], "Test"));
}

sealed class TestUsers(string role) : UserManager<ApplicationUser>(new TestUserStore(),
    Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), [], [],
    new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance)
{
    public int PasswordChanges;
    public IdentityResult PasswordResult = IdentityResult.Success;
    public override Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword)
    {
        PasswordChanges++;
        return Task.FromResult(PasswordResult);
    }
    public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
        => Task.FromResult<ApplicationUser?>(new ApplicationUser { Id = "test", FullName = "Test User", Email = "test@example.test" });
    public override Task<ApplicationUser?> FindByEmailAsync(string email)
        => Task.FromResult<ApplicationUser?>(new ApplicationUser { Id = "test", Email = email });
    public override Task<bool> IsInRoleAsync(ApplicationUser user, string requestedRole)
        => Task.FromResult(role == requestedRole);
}

sealed class TestSignIn(UserManager<ApplicationUser> users) : SignInManager<ApplicationUser>(users,
    new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
    new UserClaimsPrincipalFactory<ApplicationUser>(users, Microsoft.Extensions.Options.Options.Create(new IdentityOptions())),
    Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), NullLogger<SignInManager<ApplicationUser>>.Instance,
    new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())), new DefaultUserConfirmation<ApplicationUser>())
{
    public bool SignedOut;
    public bool Refreshed;
    public override Task RefreshSignInAsync(ApplicationUser user) { Refreshed = true; return Task.CompletedTask; }
    public override Task<Microsoft.AspNetCore.Identity.SignInResult> PasswordSignInAsync(
        ApplicationUser user, string password, bool isPersistent, bool lockoutOnFailure)
        => Task.FromResult(Microsoft.AspNetCore.Identity.SignInResult.Success);
    public override Task SignOutAsync() { SignedOut = true; return Task.CompletedTask; }
}

sealed class TestTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

sealed class TestUserStore : IUserStore<ApplicationUser>
{
    public void Dispose() { }
    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.Id);
    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.UserName);
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken token) => Task.FromResult(user.NormalizedUserName);
    public Task SetUserNameAsync(ApplicationUser user, string? value, CancellationToken token) { user.UserName = value; return Task.CompletedTask; }
    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? value, CancellationToken token) { user.NormalizedUserName = value; return Task.CompletedTask; }
    public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken token) => throw new NotSupportedException();
    public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken token) => throw new NotSupportedException();
    public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken token) => throw new NotSupportedException();
    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken token) => throw new NotSupportedException();
    public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken token) => throw new NotSupportedException();
}
