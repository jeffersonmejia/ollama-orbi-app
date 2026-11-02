using System.Data.Common;
using Npgsql;
using OrbiApp.Data;

namespace OrbiApp.Infrastructure;

public sealed class DatabaseReadinessState
{
    private int _initialized;

    public bool IsInitialized => Volatile.Read(ref _initialized) == 1;

    public void MarkInitialized() => Volatile.Write(ref _initialized, 1);
}

public sealed class DatabaseInitializationHostedService(
    IServiceProvider services,
    IHostEnvironment environment,
    DatabaseReadinessState readinessState,
    ILogger<DatabaseInitializationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = TimeSpan.FromSeconds(30);

        while (!stoppingToken.IsCancellationRequested)
        {
            var initialized = false;
            for (var attempt = 1; attempt <= 3 && !stoppingToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    await DatabaseInitializer.InitializeAsync(services, environment.ContentRootPath, stoppingToken);
                    readinessState.MarkInitialized();
                    logger.LogInformation("Database initialization completed.");
                    initialized = true;
                    break;
                }
                catch (Exception exception) when (IsDependencyFailure(exception) && !stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "Database initialization attempt failed. Attempt={Attempt} FailureType={FailureType} FailureCode={FailureCode}",
                        attempt,
                        FindFailureType(exception),
                        FindFailureCode(exception));

                    if (attempt < 3)
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2), stoppingToken);
                }
            }

            if (initialized)
                return;

            try
            {
                await Task.Delay(retryDelay, stoppingToken);
                retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 120));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static bool IsDependencyFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException or HttpRequestException)
                return true;
        }

        return false;
    }

    private static string FindFailureType(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or TimeoutException or HttpRequestException)
                return current.GetType().Name;
        }

        return exception.GetType().Name;
    }

    private static string? FindFailureCode(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
                return postgresException.SqlState;
        }

        return null;
    }
}
