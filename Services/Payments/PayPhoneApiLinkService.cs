using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SakilaApp.Settings;

namespace SakilaApp.Services.Payments;

public class PayPhoneApiLinkService
{
    private readonly HttpClient _httpClient;
    private readonly PayPhoneSettings _settings;

    public PayPhoneApiLinkService(
        HttpClient httpClient,
        IOptions<PayPhoneSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task<string> CreatePaymentLinkAsync(
        decimal total,
        string clientTransactionId,
        string reference)
    {
        int amountInCents = (int)Math.Round(
            total * 100,
            MidpointRounding.AwayFromZero);

        var request = new
{
    amount = amountInCents,
    amountWithoutTax = amountInCents,
    amountWithTax = 0,
    tax = 0,
    service = 0,
    tip = 0,
    currency = "USD",
    reference = reference,
    clientTransactionId = clientTransactionId,
    additionalData = reference,
    oneTime = true,
    expireIn = 0,
    isAmountEditable = false
};

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://pay.payphonetodoesposible.com/api/Links");

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.Token);

        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"PayPhone respondió con error: {content}");
        }

        return content.Trim('"');
    }
}