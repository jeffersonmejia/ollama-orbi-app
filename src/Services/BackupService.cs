using System.Diagnostics;
using Npgsql;

namespace SakilaApp.Services;

public sealed class BackupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BackupService> _logger;
    private static readonly TimeSpan Interval = ReadInterval();
    private static readonly int RetentionCount = ReadRetention();

    public static DateTime LastBackupUtc { get; private set; } = DateTime.UtcNow;
    public static DateTime NextBackupUtc => LastBackupUtc + Interval;

    private static TimeSpan ReadInterval()
    {
        var raw = Environment.GetEnvironmentVariable("BACKUP_INTERVAL_MINUTES");
        if (int.TryParse(raw, out var minutes) && minutes > 0)
            return TimeSpan.FromMinutes(minutes);
        return TimeSpan.FromMinutes(5);
    }

    private static int ReadRetention()
    {
        var raw = Environment.GetEnvironmentVariable("BACKUP_RETENTION_COUNT");
        if (int.TryParse(raw, out var count) && count > 0)
            return count;
        return 12;
    }

    public BackupService(IServiceProvider services, ILogger<BackupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = LastBackupUtc + Interval - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await RunBackupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup failed");
            }
        }
    }

    private async Task RunBackupAsync()
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrEmpty(conn)) return;

        var builder = new NpgsqlConnectionStringBuilder(conn);
        var backupDir = Environment.GetEnvironmentVariable("BACKUP_DIR") ?? "/backups";
        Directory.CreateDirectory(backupDir);

        var file = Path.Combine(backupDir, $"orbi_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");

        var args = $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -d {builder.Database} -f \"{file}\" --no-owner --no-privileges";

        var psi = new ProcessStartInfo("pg_dump", args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Environment = { ["PGPASSWORD"] = builder.Password }
        };

        using var process = Process.Start(psi);
        if (process == null) return;

        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var error = await errorTask;

        if (process.ExitCode != 0)
            _logger.LogWarning("pg_dump exited {Code}: {Error}", process.ExitCode, error);
        else
            _logger.LogInformation("Backup created: {File}", file);

        LastBackupUtc = DateTime.UtcNow;

        CleanupOldBackups(backupDir);
    }

    private void CleanupOldBackups(string dir)
    {
        var files = Directory.GetFiles(dir, "orbi_backup_*.sql")
            .OrderByDescending(f => f)
            .Skip(RetentionCount)
            .ToList();

        foreach (var f in files)
        {
            try { File.Delete(f); } catch { }
        }
    }
}
