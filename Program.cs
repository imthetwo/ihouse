
using ihouse.Data;
using ihouse.Models;
using ihouse.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// =====================================
// MVC
// =====================================
builder.Services.AddControllersWithViews();

// =====================================
// DATABASE - POSTGRESQL
// =====================================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Database connection is not configured.");

    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.CommandTimeout(30);
    });
});

// =====================================
// ASP.NET CORE IDENTITY
// =====================================
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// =====================================
// AUTHENTICATION COOKIE
// =====================================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// =====================================
// EMAIL SERVICE
// =====================================
builder.Services.AddScoped<EmailService>();

// =====================================
// PAYPAL SANDBOX SERVICE
// =====================================
builder.Services.AddHttpClient<PayPalService>(client =>
{
    client.BaseAddress = new Uri(
        "https://api-m.sandbox.paypal.com/");

    client.Timeout = TimeSpan.FromSeconds(30);
});

// =====================================
// IDENTITY TOKEN LIFESPAN
// =====================================
builder.Services.Configure<DataProtectionTokenProviderOptions>(
    options =>
    {
        options.TokenLifespan = TimeSpan.FromHours(24);
    });

// =====================================
// RATE LIMITING
// =====================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",

            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }
        )
    );
});

// =====================================
// BUILD APPLICATION
// =====================================
var app = builder.Build();

// =====================================
// GLOBAL ERROR HANDLING
// =====================================
// Always show a friendly error page,
// including in Development.
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/Status", "?code={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// =====================================
// STATIC FILES
// =====================================
app.UseStaticFiles();

// =====================================
// DEVELOPMENT DATA
// =====================================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await DataRoomImages.SeedAsync(db);
}

// =====================================
// CREATE IDENTITY ROLES
// =====================================
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles =
    {
        "Admin",
        "Staff",
        "Tenant"
    };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(
                new IdentityRole(role));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Cannot create role: {role}");
            }
        }
    }
}

// =====================================
// MIDDLEWARE
// =====================================
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// =====================================
// STATIC ASSETS
// =====================================
app.MapStaticAssets();

// =====================================
// MVC ROUTING
// =====================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
.WithStaticAssets();

// =====================================
// START APPLICATION
// =====================================
app.Run();
