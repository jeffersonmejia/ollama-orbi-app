using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrbiApp.Data;
using OrbiApp.Infrastructure;

namespace OrbiApp.Presentation;

public sealed class DatabaseAvailabilityHealthCheck(
    IServiceScopeFactory scopeFactory,
    DatabaseReadinessState readinessState) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!readinessState.IsInitialized)
            return HealthCheckResult.Unhealthy("Not ready");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Database.CanConnectAsync(timeout.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Dependency unavailable");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Dependency unavailable");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Dependency unavailable");
        }
    }
}
