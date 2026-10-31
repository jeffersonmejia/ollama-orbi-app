namespace OrbiApp.Application.Features.Operations.Ports;

public interface IBackupManager
{
    DateTime LastBackupUtc { get; }
    DateTime NextBackupUtc { get; }
    IReadOnlyList<BackupInfo> GetBackups();
    Task<BackupRestoreResult> RestoreAsync(string backupName, CancellationToken cancellationToken);
}

public sealed record BackupInfo(string FileName, long SizeBytes, DateTime LastWriteTimeUtc);
public sealed record BackupRestoreResult(string FileName, DateTime BackupUtc, string SafetyBackupFileName);
