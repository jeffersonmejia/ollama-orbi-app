using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SakilaApp.Settings;

namespace SakilaApp.Services.Payments;

public class PayPalOrderResult
{
    public string OrderId { get; set; } = string.Empty;
    public string ApprovalUrl { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}

public class PayPalCaptureResult
{
    public string Status { get; set; } = string.Empty;
    public string CaptureId { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}

public class PayPalService
{
    private readonly HttpClient _httpClient;
    private readonly PayPalSettings _settings;

    public PayPalService(HttpClient httpClient, IOptions<PayPalSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task<PayPalOrderResult> CreateOrderAsync(decimal total, string reference)
    {
        var accessToken = await GetAccessTokenAsync();

        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = reference,
                    description = reference,
                    amount = new
                    {
                        currency_code = "USD",
                        value = total.ToString("0.00", CultureInfo.InvariantCulture)
                    }
                }
            },
            application_context = new
            {
                brand_name = "SakilaApp ESPE",
                landing_page = "LOGIN",
                user_action = "PAY_NOW",
                return_url = _settings.ReturnUrl,
                cancel_url = _settings.CancelUrl
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_settings.BaseUrl}/v2/checkout/orders");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"PayPal respondio con error: {content}");
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var orderId = root.GetProperty("id").GetString() ?? string.Empty;
        var approvalUrl = root.GetProperty("links")
            .EnumerateArray()
            .First(link => link.GetProperty("rel").GetString() == "approve")
            .GetProperty("href")
            .GetString() ?? string.Empty;

        return new PayPalOrderResult
        {
            OrderId = orderId,
            ApprovalUrl = approvalUrl,
            RawResponse = content
        };
    }

    public async Task<PayPalCaptureResult> CaptureOrderAsync(string orderId)
    {
        var accessToken = await GetAccessTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_settings.BaseUrl}/v2/checkout/orders/{orderId}/capture");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"PayPal respondio con error al capturar: {content}");
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        string status = root.GetProperty("status").GetString() ?? string.Empty;
        string captureId = string.Empty;

        if (root.TryGetProperty("purchase_units", out var units))
        {
            var firstUnit = units.EnumerateArray().FirstOrDefault();
            if (firstUnit.ValueKind != JsonValueKind.Undefined &&
                firstUnit.TryGetProperty("payments", out var payments) &&
                payments.TryGetProperty("captures", out var captures))
            {
                var firstCapture = captures.EnumerateArray().FirstOrDefault();
                if (firstCapture.ValueKind != JsonValueKind.Undefined &&
                    firstCapture.TryGetProperty("id", out var idElement))
                {
                    captureId = idElement.GetString() ?? string.Empty;
                }
            }
        }

        return new PayPalCaptureResult
        {
            Status = status,
            CaptureId = captureId,
            RawResponse = content
        };
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_settings.BaseUrl}/v1/oauth2/token");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" }
        });

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"No se pudo obtener token PayPal: {content}");
        }

        using var document = JsonDocument.Parse(content);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("PayPal no devolvio access_token.");
    }
}