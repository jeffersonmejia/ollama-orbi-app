using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Integration.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OrbiApp.Performance.Tests;

[Collection(PostgresDatabaseCollection.Name)]
public sealed class DatabasePerformanceTests(PostgresDatabaseFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task Read_query_p95_stays_below_500ms_on_isolated_postgres()
    {
        var scenarios = new (string Name, Func<Task> Run)[]
        {
            ("catalog count + page", CatalogSearchAsync), ("address list + location joins", AddressLookupAsync),
            ("customer orders + items", CustomerOrdersAsync), ("customer payments + orders", CustomerPaymentsAsync),
            ("operations aggregates", OperationsAggregatesAsync)
        };
        foreach (var scenario in scenarios)
        {
            for (var warmup = 0; warmup < 3; warmup++) await scenario.Run();
            var samples = new double[30];
            for (var i = 0; i < samples.Length; i++)
            {
                var timer = Stopwatch.StartNew(); await scenario.Run(); samples[i] = timer.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            var stats = new PerformanceStats(samples.Min(), samples.Average(),
                DatabaseTestQueries.Percentile(samples, .50), DatabaseTestQueries.Percentile(samples, .95),
                DatabaseTestQueries.Percentile(samples, .99), samples.Max(), samples.Length);
            output.WriteLine($"{scenario.Name}: min={stats.MinMs:F2} avg={stats.AverageMs:F2} p50={stats.P50Ms:F2} p95={stats.P95Ms:F2} p99={stats.P99Ms:F2} max={stats.MaxMs:F2} ms, n={stats.Samples}");
            await PerformanceReport.WriteAsync("database", scenario.Name, stats);
            Assert.True(stats.P95Ms < 500, $"{scenario.Name} p95 was {stats.P95Ms:F2} ms (limit 500 ms).");
        }
    }

    private async Task CatalogSearchAsync()
    {
        await using var db = fixture.CreateContext();
        var query = DatabaseTestQueries.Catalog(db).Where(product => product.Store.CityCode == fixture.CityCode)
            .Where(product => EF.Functions.ILike(product.Name, "%catalog-hit%") ||
                              EF.Functions.ILike(product.Store.Name, "%catalog-hit%") ||
                              EF.Functions.ILike(product.Store.Category, "%catalog-hit%"));
        _ = await query.CountAsync();
        _ = await query.OrderBy(product => product.Store.Name).ThenBy(product => product.Name).Take(12).ToListAsync();
    }

    private async Task AddressLookupAsync()
    {
        await using var db = fixture.CreateContext();
        _ = await db.UserAddresses.AsNoTracking().Include(a => a.Province).Include(a => a.City)
            .Where(a => a.IdentityUserId == DatabaseTestQueries.MissingUserId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.UserAddressId).ToListAsync();
    }

    private async Task CustomerOrdersAsync()
    {
        await using var db = fixture.CreateContext();
        _ = await db.DeliveryOrders.AsNoTracking().Include(o => o.Store).Include(o => o.Items)
            .Where(o => o.CustomerEmail == DatabaseTestQueries.MissingEmail).OrderByDescending(o => o.CreatedAt).Take(12).ToListAsync();
    }

    private async Task CustomerPaymentsAsync()
    {
        await using var db = fixture.CreateContext();
        _ = await db.DeliveryPayments.AsNoTracking().Include(p => p.Order).ThenInclude(o => o.Store)
            .Where(p => p.Order.CustomerEmail == DatabaseTestQueries.MissingEmail).OrderByDescending(p => p.CreatedAt).Take(12).ToListAsync();
    }

    private async Task OperationsAggregatesAsync()
    {
        await using var db = fixture.CreateContext();
        _ = await db.InventoryMovements.CountAsync();
        _ = await db.StockReservations.CountAsync(r => r.Status == "Activa");
        _ = await db.DeliveryIncidents.CountAsync(i => i.Status == "Abierto" || i.Status == "En revisión");
        _ = await db.OrderStatusHistories.CountAsync();
        _ = await db.EmailQueue.CountAsync(q => q.Status == "Pendiente" || q.Status == "Fallido");
        _ = await db.AuditLogs.CountAsync();
    }
}
