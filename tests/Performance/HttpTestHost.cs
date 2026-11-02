using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrbiApp.Controllers;
using OrbiApp.Integration.Tests;
using OrbiApp.Models.Delivery;

namespace OrbiApp.Performance.Tests;

internal sealed class HttpTestHost : IAsyncDisposable
{
    private readonly PostgresDatabaseFixture _database = new();
    private WebApplicationFactory<DeliveryCatalogApiController>? _factory;

    public string CatalogPath => $"/api/delivery/catalog?buscar={Uri.EscapeDataString(_database.TestDataPrefix)}&cityCode={_database.CityCode}&page=1";
    public string OrdersPath => $"/api/delivery/orders?buscar={Uri.EscapeDataString(_database.TestDataPrefix)}&page=1";
    public string OrdersPageTwoPath => $"/api/delivery/orders?buscar={Uri.EscapeDataString(_database.TestDataPrefix)}&page=2";
    public string EmptyOrdersPath => $"/api/delivery/orders?buscar=missing-{Uri.EscapeDataString(_database.TestDataPrefix)}&page=1";
    public string PaymentsPath => $"/api/delivery/payments?buscar={Uri.EscapeDataString(_database.TestDataPrefix)}&page=1";
    public string PaymentsPageTwoPath => $"/api/delivery/payments?buscar={Uri.EscapeDataString(_database.TestDataPrefix)}&page=2";
    public string EmptyPaymentsPath => $"/api/delivery/payments?buscar=missing-{Uri.EscapeDataString(_database.TestDataPrefix)}&page=1";
    public const string OperationsPath = "/Operations/InventoryMovements?page=1";
    public string Prefix => _database.TestDataPrefix;
    public string CustomerEmail { get; private set; } = string.Empty;
    public HttpClient Client { get; private set; } = null!;

    public async Task StartAsync()
    {
        await _database.InitializeAsync();
        CustomerEmail = $"performance-{_database.TestDataPrefix.TrimEnd('-')}@invalid.test";
        await SeedOrdersAndPaymentsAsync();
        var connectionString = Environment.GetEnvironmentVariable("ORBI_DATABASE_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set ORBI_DATABASE_TEST_CONNECTION to the isolated local audit database.");
        _factory = new WebApplicationFactory<DeliveryCatalogApiController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = LoadTestAuthenticationHandler.AuthenticationSchemeName;
                    options.DefaultChallengeScheme = LoadTestAuthenticationHandler.AuthenticationSchemeName;
                    options.DefaultScheme = LoadTestAuthenticationHandler.AuthenticationSchemeName;
                }).AddScheme<AuthenticationSchemeOptions, LoadTestAuthenticationHandler>(
                    LoadTestAuthenticationHandler.AuthenticationSchemeName, _ => { });
            });
        });
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Client.DefaultRequestHeaders.Add("X-Orbi-Test-Email", CustomerEmail);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (!string.IsNullOrWhiteSpace(CustomerEmail)) await RemoveOrdersAndPaymentsAsync();
        await _database.DisposeAsync();
    }

    private async Task SeedOrdersAndPaymentsAsync()
    {
        await using var db = _database.CreateContext();
        var product = await db.DeliveryProducts.AsNoTracking()
            .FirstAsync(item => item.Name.StartsWith(_database.TestDataPrefix));
        for (var index = 0; index < 13; index++)
        {
            var order = new DeliveryOrder
            {
                DeliveryStoreId = product.DeliveryStoreId,
                CustomerEmail = CustomerEmail,
                DeliveryPersonEmail = CustomerEmail,
                DeliveryAddress = $"Dirección de prueba {index}",
                Status = "Pendiente",
                Total = product.Price * 2,
                CreatedAt = DateTime.UtcNow.AddMinutes(-index),
                Items = [new DeliveryOrderItem
                {
                    DeliveryProductId = product.DeliveryProductId,
                    ProductName = product.Name,
                    Quantity = 2,
                    UnitPrice = product.Price,
                    Subtotal = product.Price * 2
                }]
            };
            order.Payments.Add(new DeliveryPayment
            {
                ExternalId = $"perf-{Guid.NewGuid():N}",
                Provider = "TestGateway",
                Status = "Aprobado",
                Amount = order.Total
            });
            db.DeliveryOrders.Add(order);
        }
        await db.SaveChangesAsync();
    }

    private async Task RemoveOrdersAndPaymentsAsync()
    {
        await using var db = _database.CreateContext();
        var orders = await db.DeliveryOrders.Where(order => order.CustomerEmail == CustomerEmail).ToListAsync();
        var orderIds = orders.Select(order => order.DeliveryOrderId).ToArray();
        var payments = await db.DeliveryPayments.Where(payment => orderIds.Contains(payment.DeliveryOrderId)).ToListAsync();
        db.DeliveryPayments.RemoveRange(payments);
        db.DeliveryOrders.RemoveRange(orders);
        await db.SaveChangesAsync();
    }
}

internal sealed class LoadTestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string AuthenticationSchemeName = "OrbiIsolatedLoadTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "orbi-performance-user"),
            new Claim(ClaimTypes.Name, Context.Request.Headers["X-Orbi-Test-Email"].FirstOrDefault() ?? "orbi-performance-user@invalid.test"),
            new Claim(ClaimTypes.Email, Context.Request.Headers["X-Orbi-Test-Email"].FirstOrDefault() ?? "orbi-performance-user@invalid.test"),
            new Claim(ClaimTypes.Role, "Usuario"),
            new Claim(ClaimTypes.Role, "Repartidor"),
            new Claim(ClaimTypes.Role, "Administrador")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationSchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, AuthenticationSchemeName)));
    }
}
