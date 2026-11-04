namespace OrbiApp.LoadTestRunner;

public sealed record LabEvent(
    DateTimeOffset AtUtc,
    string Kind,
    string Status,
    string Message,
    string? RunId = null,
    int? Replicas = null,
    int? Vus = null,
    string? Version = null);

public sealed record RunMetadata
{
    public required string RunId { get; init; }
    public required string Profile { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required string GitCommit { get; init; }
    public required string AppImage { get; init; }
    public required int ReplicasRequested { get; init; }
    public required int ReplicasReady { get; init; }
    public required int Vus { get; init; }
    public double? VusObserved { get; init; }
    public required int DurationSeconds { get; init; }
    public double? DurationObservedSeconds { get; init; }
    public required string Environment { get; init; }
    public required string OperatingSystem { get; init; }
    public required int ProcessorCount { get; init; }
    public required string RunStatus { get; init; }
    public required string ThresholdStatus { get; init; }
    public required int ExitCode { get; init; }
    public required Dictionary<string, string> ToolVersions { get; init; }
    public required string K6Image { get; init; }
    public string? RecoveryPod { get; init; }
    public double? RecoverySeconds { get; init; }
    public string? Notes { get; init; }
}

public sealed record RunMetrics(
    double? TotalRequests,
    double? RequestsPerSecond,
    double? ErrorRate,
    double? AverageMilliseconds,
    double? P95Milliseconds,
    double? P99Milliseconds,
    double? ChecksRate,
    double? ObservedVus,
    double? ObservedDurationSeconds);
