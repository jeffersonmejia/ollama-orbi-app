using System.Diagnostics;
using Npgsql;

namespace SakilaApp.Services;

public sealed class BackupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BackupService> _logger;
    private static readonly TimeSpan Interval = ReadInterval();
    private static readonly int RetentionCount = ReadRetention();
    private static readonly SemaphoreSlim OperationLock = new(1, 1);

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
        await OperationLock.WaitAsync();
        try
        {
            await RunBackupCoreAsync();
        }
        finally
        {
            OperationLock.Release();
        }
    }

    private async Task RunBackupCoreAsync()
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrEmpty(conn)) return;

        var builder = new NpgsqlConnectionStringBuilder(conn);
        var backupDir = GetBackupDirectory();
        Directory.CreateDirectory(backupDir);

        var file = Path.Combine(backupDir, $"orbi_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");
        var result = await RunPostgresToolAsync("pg_dump", builder, new[]
        {
            "-f", file, "--no-owner", "--no-privileges"
        }, CancellationToken.None);

        if (!result.Success)
        {
            _logger.LogWarning("pg_dump exited {Code}: {Error}", result.ExitCode, result.Error);
            return;
        }

        _logger.LogInformation("Backup created: {File}", file);
        LastBackupUtc = DateTime.UtcNow;

        CleanupOldBackups(backupDir);
    }

    public static FileInfo? GetLatestBackup()
        => GetBackups().FirstOrDefault();

    public static IReadOnlyList<FileInfo> GetBackups()
    {
        try
        {
            var dir = GetBackupDirectory();
            if (!Directory.Exists(dir)) return Array.Empty<FileInfo>();
            return new DirectoryInfo(dir)
                .GetFiles("orbi_backup_*.sql")
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();
        }
        catch
        {
            return Array.Empty<FileInfo>();
        }
    }

    public static async Task<BackupRestoreResult> RestoreAsync(
        string backupName,
        CancellationToken cancellationToken)
    {
        await OperationLock.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(backupName) || Path.GetFileName(backupName) != backupName)
                throw new InvalidOperationException("El backup seleccionado no es válido.");
            var selected = GetBackups().FirstOrDefault(file =>
                string.Equals(file.Name, backupName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("El backup seleccionado ya no está disponible.");
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("No se encontró la conexión a PostgreSQL.");

            var builder = new NpgsqlConnectionStringBuilder(connection);
            var safetyFile = Path.Combine(
                GetBackupDirectory(),
                $"orbi_pre_restore_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");

            var safety = await RunPostgresToolAsync("pg_dump", builder, new[]
            {
                "-f", safetyFile, "--no-owner", "--no-privileges"
            }, cancellationToken);
            if (!safety.Success)
                throw new InvalidOperationException($"No se pudo crear el respaldo de seguridad: {safety.Error}");

            var reset = await ResetPublicSchemaAsync(builder, cancellationToken);
            if (!reset.Success)
                throw new InvalidOperationException($"No se pudo preparar la base de datos: {reset.Error}");

            var restore = await RunPostgresToolAsync("psql", builder, new[]
            {
                "-X", "-v", "ON_ERROR_STOP=1", "-f", selected.FullName
            }, cancellationToken);

            if (!restore.Success)
            {
                await ResetPublicSchemaAsync(builder, CancellationToken.None);
                var rollback = await RunPostgresToolAsync("psql", builder, new[]
                {
                    "-X", "-v", "ON_ERROR_STOP=1", "-f", safetyFile
                }, CancellationToken.None);
                var rollbackMessage = rollback.Success
                    ? " Se recuperó automáticamente el estado anterior."
                    : " También falló la recuperación del estado anterior.";
                throw new InvalidOperationException($"Falló la recuperación del backup.{rollbackMessage}");
            }

            LastBackupUtc = DateTime.UtcNow;
            return new BackupRestoreResult(selected.Name, selected.LastWriteTimeUtc, Path.GetFileName(safetyFile));
        }
        finally
        {
            OperationLock.Release();
        }
    }

    private static async Task<PostgresToolResult> ResetPublicSchemaAsync(
        NpgsqlConnectionStringBuilder builder,
        CancellationToken cancellationToken) =>
        await RunPostgresToolAsync("psql", builder, new[]
        {
            "-X", "-v", "ON_ERROR_STOP=1", "-c",
            "DROP SCHEMA public CASCADE; CREATE SCHEMA public; GRANT ALL ON SCHEMA public TO PUBLIC;"
        }, cancellationToken);

    private static async Task<PostgresToolResult> RunPostgresToolAsync(
        string executable,
        NpgsqlConnectionStringBuilder builder,
        IEnumerable<string> extraArguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.Environment["PGPASSWORD"] = builder.Password ?? string.Empty;
        foreach (var argument in new[]
        {
            "-h", builder.Host ?? string.Empty,
            "-p", builder.Port.ToString(),
            "-U", builder.Username ?? string.Empty,
            "-d", builder.Database ?? string.Empty
        }.Concat(extraArguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"No se pudo iniciar {executable}.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await outputTask;
        var error = await errorTask;
        return new PostgresToolResult(process.ExitCode == 0, process.ExitCode, error.Trim());
    }

    private static string GetBackupDirectory() =>
        Path.GetFullPath(Environment.GetEnvironmentVariable("BACKUP_DIR") ?? "/backups");

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

    private sealed record PostgresToolResult(bool Success, int ExitCode, string Error);
}

public sealed record BackupRestoreResult(string FileName, DateTime BackupUtc, string SafetyBackupFileName);
