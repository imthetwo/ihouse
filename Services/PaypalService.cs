using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ihouse.Services;

public sealed record PayPalOrderDetails
{
    public string OrderId { get; init; } = "";
    public string Status { get; init; } = "";
    public string? ApprovalUrl { get; init; }
    public string? CaptureId { get; init; }
    public string? CaptureStatus { get; init; }
    public decimal? CapturedAmount { get; init; }
    public decimal OrderAmount { get; init; }
    public string Currency { get; init; } = "";
    public string? ReferenceId { get; init; }

    public bool IsCaptured =>
        Status == "COMPLETED" &&
        CaptureStatus == "COMPLETED" &&
        !string.IsNullOrWhiteSpace(CaptureId) &&
        CapturedAmount.HasValue;

    public bool IsFailed =>
        Status == "VOIDED" ||
        CaptureStatus is "DECLINED" or "FAILED" or "DENIED";
}

public sealed class PayPalService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayPalService> _logger;

    public PayPalService(
        HttpClient http,
        IConfiguration configuration,
        ILogger<PayPalService> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    private string GetSetting(string name)
    {
        return _configuration[$"PayPal:{name}"]
            ?? throw new InvalidOperationException(
                $"Missing PayPal:{name} configuration.");
    }

    private async Task<string> GetAccessTokenAsync()
    {
        string credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{GetSetting("ClientId")}:{GetSetting("ClientSecret")}"));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v1/oauth2/token");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                credentials);

        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            });

        using var response = await _http.SendAsync(request);

        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return json.RootElement
            .GetProperty("access_token")
            .GetString()
            ?? throw new InvalidOperationException(
                "PayPal access token is missing.");
    }

    private async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string endpoint,
        object? body = null,
        string? requestId = null)
    {
        string accessToken = await GetAccessTokenAsync();

        using var request = new HttpRequestMessage(
            method,
            endpoint);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.Add(
            "Prefer",
            "return=representation");

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            request.Headers.Add(
                "PayPal-Request-Id",
                requestId);
        }

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _http.SendAsync(request);

        string responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "PayPal API request failed. Endpoint: {Endpoint}, Status: {Status}, Response: {Response}",
                endpoint,
                response.StatusCode,
                responseBody);

            throw new HttpRequestException(
                "PayPal could not process the request.", null, response.StatusCode);
        }

        return JsonDocument.Parse(responseBody);
    }

    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static decimal ReadAmount(JsonElement amount)
    {
        string value = amount
            .GetProperty("value")
            .GetString()
            ?? throw new InvalidOperationException(
                "Invalid PayPal amount.");

        return decimal.Parse(
            value,
            CultureInfo.InvariantCulture);
    }

    private static string? FindApprovalUrl(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links))
            return null;

        foreach (var link in links.EnumerateArray())
        {
            string? relation = GetString(link, "rel");

            if (relation is "payer-action" or "approve")
            {
                return GetString(link, "href");
            }
        }

        return null;
    }

    private static bool IsValidApprovalUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps &&
               (
                   uri.Host.Equals(
                       "paypal.com",
                       StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.EndsWith(
                       ".paypal.com",
                       StringComparison.OrdinalIgnoreCase)
               );
    }

    private static PayPalOrderDetails ParseOrder(
        JsonElement root)
    {
        string orderId = GetString(root, "id") ?? "";

        string status = GetString(root, "status") ?? "";

        var purchaseUnits = root.GetProperty("purchase_units");

        if (purchaseUnits.GetArrayLength() != 1)
        {
            throw new InvalidOperationException(
                "Unexpected PayPal purchase units.");
        }

        var purchaseUnit = purchaseUnits[0];

        var amount = purchaseUnit.GetProperty("amount");

        string currency = GetString(
            amount,
            "currency_code") ?? "";

        decimal orderAmount = ReadAmount(amount);

        string? captureId = null;
        string? captureStatus = null;
        decimal? capturedAmount = null;

        if (purchaseUnit.TryGetProperty(
                "payments",
                out var payments) &&
            payments.TryGetProperty(
                "captures",
                out var captures) &&
            captures.ValueKind == JsonValueKind.Array)
        {
            foreach (var capture in captures.EnumerateArray())
            {
                string? currentStatus =
                    GetString(capture, "status");

                if (currentStatus == "COMPLETED")
                {
                    captureId = GetString(capture, "id");
                    captureStatus = currentStatus;

                    capturedAmount = ReadAmount(
                        capture.GetProperty("amount"));

                    break;
                }

                captureId = GetString(capture, "id");
                captureStatus = currentStatus;

                if (capture.TryGetProperty(
                        "amount",
                        out var captureAmount))
                {
                    capturedAmount = ReadAmount(captureAmount);
                }
            }
        }

        return new PayPalOrderDetails
        {
            OrderId = orderId,
            Status = status,
            ApprovalUrl = FindApprovalUrl(root),
            ReferenceId = GetString(
                purchaseUnit,
                "reference_id"),
            Currency = currency,
            OrderAmount = orderAmount,
            CaptureId = captureId,
            CaptureStatus = captureStatus,
            CapturedAmount = capturedAmount
        };
    }

    public async Task<(string OrderId, string ApprovalUrl)>
        CreateOrderAsync(
            decimal amount,
            int invoiceId,
            string returnUrl,
            string cancelUrl)
    {
        if (amount <= 0 ||
            decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount));
        }

        var payload = new
        {
            intent = "CAPTURE",

            purchase_units = new[]
            {
                new
                {
                    reference_id = $"IHOUSE-{invoiceId}",

                    description = $"iHouse Invoice #{invoiceId}",

                    amount = new
                    {
                        currency_code = "USD",

                        value = amount.ToString(
                            "F2",
                            CultureInfo.InvariantCulture)
                    }
                }
            },

            payment_source = new
            {
                paypal = new
                {
                    experience_context = new
                    {
                        user_action = "PAY_NOW",
                        return_url = returnUrl,
                        cancel_url = cancelUrl
                    }
                }
            }
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            "v2/checkout/orders",
            payload,
            Guid.NewGuid().ToString("N"));

        var order = ParseOrder(response.RootElement);

        if (string.IsNullOrWhiteSpace(order.OrderId) ||
            !IsValidApprovalUrl(order.ApprovalUrl))
        {
            throw new InvalidOperationException(
                "Could not create a valid PayPal checkout.");
        }

        return (
            order.OrderId,
            order.ApprovalUrl!
        );
    }

    public async Task<PayPalOrderDetails> GetOrderAsync(
        string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException(
                "Order ID is required.",
                nameof(orderId));
        }

        using var response = await SendAsync(
            HttpMethod.Get,
            $"v2/checkout/orders/{Uri.EscapeDataString(orderId)}");

        var result = ParseOrder(response.RootElement);

        if (result.OrderId != orderId)
        {
            throw new InvalidOperationException(
                "PayPal order ID mismatch.");
        }

        return result;
    }

    public async Task<PayPalOrderDetails> CaptureOrderAsync(
        string orderId)
    {
        // Check the existing order first.
        // Never capture an order that already has a completed payment.
        var existing = await GetOrderAsync(orderId);

        if (existing.IsCaptured ||
            existing.IsFailed ||
            existing.Status == "COMPLETED")
        {
            return existing;
        }

        if (existing.Status != "APPROVED")
        {
            return existing;
        }

        try
        {
            using var response = await SendAsync(
                HttpMethod.Post,
                $"v2/checkout/orders/{Uri.EscapeDataString(orderId)}/capture",
                new { },
                $"ihouse-capture-{orderId}");

            var captured = ParseOrder(response.RootElement);

            if (captured.IsCaptured || captured.IsFailed)
                return captured;

            // Check the current status once more.
            return await GetOrderAsync(orderId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "PayPal capture response was uncertain for order {OrderId}",
                orderId);

            // A failed network response does not necessarily mean
            // the customer was not charged.
            return await GetOrderAsync(orderId);
        }
    }
}