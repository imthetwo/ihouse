using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using ihouse.Data;
using ihouse.Models;
using ihouse.Models.Enums;
using ihouse.Services;
using ihouse.ViewModels.Payment;
using ihouse.ViewModels.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Razor.Hosting;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

bool Valid(object model) => Validator.TryValidateObject(model,
    new ValidationContext(model), new List<ValidationResult>(), true);

var cash = new PaymentFormViewModel
{
    InvoiceId = 1, Amount = 0.014m, PaymentDate = DateTime.UtcNow,
    PaymentMethod = PaymentMethod.Cash
};
Check(!Valid(cash), "Reject cash fractions smaller than a cent");
cash.Amount = 0.01m;
Check(Valid(cash), "Accept one cent cash payment");
cash.PaymentMethod = PaymentMethod.PayPal;
Check(!Valid(cash), "Reject manual PayPal receipts");
var cost = new MaintenanceCostFormViewModel { Description = "Repair", Amount = 1.005m };
Check(!Valid(cost), "Reject maintenance amounts with excess precision");
cost.Amount = -1;
Check(!Valid(cost), "Reject negative maintenance charges");
cost.Amount = 15.25m;
Check(Valid(cost), "Accept valid maintenance charges");

Check(InvoiceCalculator.CalculateAmount(12.5m, 0.3m) == 3.75m, "Calculate decimal meter consumption");
Check(InvoiceCalculator.CalculateAmount(1.005m, 1m) == 1.01m, "Round service charges consistently to cents");
try
{
    InvoiceCalculator.CalculateAmount(-1m, 1m);
    Check(false, "Reject decreasing meter consumption");
}
catch (ArgumentOutOfRangeException) { Check(true, "Reject decreasing meter consumption"); }

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
using var db = new ApplicationDbContext(options);
bool Unique<T>(params string[] names) => db.Model.FindEntityType(typeof(T))!.GetIndexes()
    .Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(names));
Check(Unique<Invoice>("ContractId", "BillingMonth"), "Unique invoice per contract and month");
Check(Unique<MeterReading>("RoomId", "ReadingDay"), "Unique room reading per day");
Check(Unique<Room>("RoomNumber") && Unique<Tenant>("IdentityNumber"), "Unique room and tenant identifiers");
var pendingIndex = db.Model.FindEntityType(typeof(PaymentTransaction))!.GetIndexes()
    .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == "InvoiceId");
Check(pendingIndex.IsUnique && pendingIndex.GetFilter() == "\"Status\" = 0", "Only one pending payment per invoice");
Check(Unique<Payment>("TransactionCode"), "Unique capture and receipt codes");
var migrationSql = db.GetService<IMigrator>().GenerateScript();
Check(migrationSql.Contains("EXTRACT(YEAR FROM") && migrationSql.Contains("WHERE \"Status\" = 0"),
    "Migration SQL contains billing month and pending constraints");
var views = new RazorCompiledItemLoader().LoadItems(typeof(ApplicationDbContext).Assembly);
Check(views.Any(v => v.Identifier == "/Views/Shared/Error.cshtml"), "Error view is compiled into the application");

var errorController = new ihouse.Controllers.ErrorController(
    NullLogger<ihouse.Controllers.ErrorController>.Instance)
{
    ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
    {
        HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
    }
};
errorController.HttpContext.Features.Set<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>(
    new Microsoft.AspNetCore.Diagnostics.ExceptionHandlerFeature
    {
        Error = new InvalidOperationException("private database details"), Path = "/private"
    });
var errorView = (Microsoft.AspNetCore.Mvc.ViewResult)errorController.Index();
Check(errorController.Response.StatusCode == 500 && errorView.ViewName == "Error" && errorView.Model == null,
    "Unexpected errors use the shared error view without exposing exception details");
errorController.Status(404);
Check(errorController.Response.StatusCode == 404 && (string)errorController.ViewBag.ErrorTitle == "Page not found",
    "Missing pages receive a friendly 404 response");
errorController.Status(200);
Check(errorController.Response.StatusCode == 500, "Error page rejects success status codes");

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["PayPal:ClientId"] = "test", ["PayPal:ClientSecret"] = "test"
}).Build();
var handler = new FakePayPal();
var paypal = new PayPalService(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
    config, NullLogger<PayPalService>.Instance);
handler.OrderStatus = "COMPLETED";
handler.CaptureStatus = "COMPLETED";
var captured = await paypal.CaptureOrderAsync("ORDER-1");
Check(captured.IsCaptured && captured.CapturedAmount == 10m && handler.CaptureCalls == 0,
    "Already captured orders are not captured twice");
handler.CaptureStatus = "PENDING";
Check(!(await paypal.GetOrderAsync("ORDER-1")).IsCaptured, "Pending captures are not treated as successful");
handler.OrderStatus = "APPROVED";
handler.CaptureStatus = null;
Check((await paypal.CaptureOrderAsync("ORDER-1")).IsCaptured && handler.CaptureCalls == 1,
    "Approved orders are captured once");
handler.NotFound = true;
try
{
    await paypal.GetOrderAsync("ORDER-1");
    Check(false, "Preserve PayPal not-found status");
}
catch (HttpRequestException ex)
{
    Check(ex.StatusCode == HttpStatusCode.NotFound, "Preserve PayPal not-found status");
}

Console.WriteLine($"{passed} checks passed. Database and live PayPal integration were not run.");

sealed class FakePayPal : HttpMessageHandler
{
    public string OrderStatus = "APPROVED";
    public string? CaptureStatus;
    public int CaptureCalls;
    public bool NotFound;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("oauth2/token"))
            return Task.FromResult(Json("{\"access_token\":\"test\"}"));
        if (NotFound) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        if (request.Method == HttpMethod.Post)
        {
            CaptureCalls++;
            OrderStatus = "COMPLETED";
            CaptureStatus = "COMPLETED";
        }
        var captures = CaptureStatus == null ? Array.Empty<object>() : new object[]
        {
            new { id = "CAPTURE-1", status = CaptureStatus, amount = new { value = "10.00", currency_code = "USD" } }
        };
        var order = new
        {
            id = "ORDER-1", status = OrderStatus,
            purchase_units = new[]
            {
                new
                {
                    reference_id = "IHOUSE-1", amount = new { value = "10.00", currency_code = "USD" },
                    payments = new { captures }
                }
            }
        };
        return Task.FromResult(Json(System.Text.Json.JsonSerializer.Serialize(order)));
    }

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };
}
