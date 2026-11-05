using System.Diagnostics;
using System.Text.Json;

namespace OrbiApp.LoadTestRunner;

public sealed record K6RunRequest(string Profile, int Vus, int DurationSeconds, int Replicas, bool AllowStress);

public sealed record K6RunResult(
    string RunId,
    string Directory,
    string SummaryPath,
    string StdoutPath,
    string StderrPath,
    string KubernetesBeforePath,
    string KubernetesAfterPath,
    int ExitCode,
    string Status,
    DateTimeOffset StartedAtUtc,
    RunMetrics? Metrics,
    string Notes);

public sealed class K6RunSession : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task<string> _stdout;
    private readonly Task<string> _stderr;
    private readonly string _stdoutPath;
    private readonly string _stderrPath;
    private readonly EventPublisher _events;
    private readonly CommandRunner _commands;
    private readonly K6RunRequest _request;
    private readonly string _runId;
    private readonly DateTimeOffset _started;
    private bool _finished;

    internal K6RunSession(
        Process process,
        Task<string> stdout,
        Task<string> stderr,
        string stdoutPath,
        string stderrPath,
        EventPublisher events,
        CommandRunner commands,
        K6RunRequest request,
        string runId,
        DateTimeOffset started)
    {
        _process = process;
        _stdout = stdout;
        _stderr = stderr;
        _stdoutPath = stdoutPath;
        _stderrPath = stderrPath;
        _events = events;
        _commands = commands;
        _request = request;
        _runId = runId;
        _started = started;
    }

    public string RunId => _runId;
    public DateTimeOffset StartedAtUtc => _started;
    public bool HasExited => _process.HasExited;

    public async Task<K6RunResult> WaitAsync(
        string runDirectory,
        string beforePath,
        string afterPath,
        CancellationToken cancellationToken = default)
    {
        if (_finished)
            throw new InvalidOperationException("La sesión k6 ya terminó.");
        _finished = true;
        try
        {
            await _process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await _process.WaitForExitAsync(CancellationToken.None);
            await StopContainerAsync();
            await SaveLogsAsync();
            await File.WriteAllTextAsync(Path.Combine(runDirectory, "run-status.json"),
                "{\"run_status\":\"CANCELLED\",\"exit_code\":130}", CancellationToken.None);
            await _events.PublishAsync(new LabEvent(DateTimeOffset.UtcNow, "test", "CANCELLED",
                "Corrida cancelada; se detuvo el contenedor k6 del run.", _runId, _request.Replicas, _request.Vus),
                CancellationToken.None);
            throw;
        }

        var stdout = await _stdout;
        var stderr = await _stderr;
        await File.WriteAllTextAsync(_stdoutPath, stdout, CancellationToken.None);
        await File.WriteAllTextAsync(_stderrPath, stderr, CancellationToken.None);
        var summaryPath = Path.Combine(runDirectory, "k6-summary.json");
        RunMetrics? metrics = null;
        var thresholdFailed = false;
        string? summaryError = null;
        if (!File.Exists(summaryPath))
            summaryError = "k6 no produjo el resumen JSON esperado.";
        else
        {
            try
            {
                metrics = K6Executor.ParseMetrics(summaryPath);
                thresholdFailed = K6Executor.HasFailedThresholds(summaryPath);
                summaryError = K6Executor.ValidateObservedProfile(_request, metrics);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or IOException)
            {
                summaryError = "El resumen JSON de k6 está corrupto o no cumple el formato esperado.";
            }
        }
        var status = summaryError is not null ? "INFRA_ERROR" : _process.ExitCode == 0 ? "PASS" : thresholdFailed ? "THRESHOLD_FAIL" : "INFRA_ERROR";
        var notes = summaryError ?? (_process.ExitCode == 0 ? string.Empty : thresholdFailed
            ? "k6 terminó con thresholds incumplidos."
            : string.IsNullOrWhiteSpace(stderr) ? "k6 terminó con error antes de completar el resumen." : stderr.Trim());
        var exitCode = status == "PASS" ? 0 : _process.ExitCode == 0 ? 1 : _process.ExitCode;

        await _events.PublishAsync(new LabEvent(DateTimeOffset.UtcNow, "test", status,
            $"Corrida {_request.Profile} finalizada (exit {exitCode}).", _runId,
            _request.Replicas, _request.Vus), cancellationToken);

        return new K6RunResult(_runId, runDirectory, summaryPath, _stdoutPath, _stderrPath,
            beforePath, afterPath, exitCode, status, _started, metrics, notes);
    }

    public async Task CancelAsync()
    {
        if (!_process.HasExited)
        {
            try { _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await _process.WaitForExitAsync(CancellationToken.None);
        }
        await StopContainerAsync();
        await SaveLogsAsync();
    }

    private async Task StopContainerAsync()
    {
        try
        {
            await _commands.RunAsync("docker", ["stop", "--time", "5", $"orbi-k6-{_runId}"],
                timeout: TimeSpan.FromSeconds(15), cancellationToken: CancellationToken.None);
        }
        catch (System.ComponentModel.Win32Exception) { }
    }

    private async Task SaveLogsAsync()
    {
        try { await File.WriteAllTextAsync(_stdoutPath, await _stdout, CancellationToken.None); } catch (OperationCanceledException) { }
        try { await File.WriteAllTextAsync(_stderrPath, await _stderr, CancellationToken.None); } catch (OperationCanceledException) { }
    }

    public ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
            try { _process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        _process.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class K6Executor(string repositoryRoot, CommandRunner commands, EventPublisher events)
{
    private const string Image = "grafana/k6:2.3.0";
    private static readonly IReadOnlyDictionary<string, (string Script, string Target)> Profiles =
        new Dictionary<string, (string Script, string Target)>(StringComparer.OrdinalIgnoreCase)
        {
            ["smoke"] = ("smoke.js", "/health/live"),
            ["baseline"] = ("baseline.js", "/api/identity/registration/options"),
            ["scalability"] = ("scalability.js", "/api/identity/registration/options"),
            ["availability"] = ("availability.js", "/api/identity/registration/options"),
            ["rollout"] = ("rollout.js", "/api/identity/registration/options"),
        };

    public static string ImageReference => Image;

    public async Task<K6RunSession> StartAsync(K6RunRequest request, string? runId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Profiles.TryGetValue(request.Profile, out var profile))
            throw new ArgumentException($"Perfil k6 no reconocido: {request.Profile}.");
        if (request.Vus < 1 || request.Vus > 500 || request.DurationSeconds < 1 || request.DurationSeconds > 120)
            throw new ArgumentOutOfRangeException(nameof(request), "VUs/duración fuera de los límites del laboratorio.");
        if (request.Vus > 100 && !request.AllowStress)
            throw new InvalidOperationException("El perfil de estrés requiere autorización explícita.");

        var id = runId ?? CreateRunId(request.Replicas, request.Vus);
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$"))
            throw new ArgumentException("Run ID no válido.", nameof(runId));
        var started = DateTimeOffset.UtcNow;
        var resultsRoot = Path.Combine(repositoryRoot, "tests", "k6", "results");
        var runDirectory = Path.Combine(resultsRoot, id);
        Directory.CreateDirectory(runDirectory);
        var beforePath = Path.Combine(runDirectory, "kubernetes-before.json");
        var afterPath = Path.Combine(runDirectory, "kubernetes-after.json");
        var stdoutPath = Path.Combine(runDirectory, "stdout.log");
        var stderrPath = Path.Combine(runDirectory, "stderr.log");
        var composeFiles = new[] { "docker-compose.yml", "docker-compose.monitoring.yml", "docker-compose.k6.yml" };

        var startInfo = new ProcessStartInfo("docker")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("compose");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add("orbi-app");
        foreach (var composeFile in composeFiles)
        {
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(composeFile);
        }
        cancellationToken.ThrowIfCancellationRequested();
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--rm");
        startInfo.ArgumentList.Add("--no-deps");
        startInfo.ArgumentList.Add("--name");
        startInfo.ArgumentList.Add($"orbi-k6-{id}");
        AddEnv(startInfo, "ORBI_K6_BASE_URL", Environment.GetEnvironmentVariable("ORBI_LAB_K6_BASE_URL") ?? "http://host.docker.internal:30083");
        AddEnv(startInfo, "ORBI_K6_RUN_ID", id);
        AddEnv(startInfo, "ORBI_K6_PROFILE", request.Profile);
        AddEnv(startInfo, "ORBI_K6_VUS_TARGET", request.Vus.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AddEnv(startInfo, "ORBI_K6_DURATION", $"{request.DurationSeconds}s");
        AddEnv(startInfo, "ORBI_K6_TARGET_PATH", profile.Target);
        AddEnv(startInfo, "ORBI_K6_TIMEOUT", "10s");
        AddEnv(startInfo, "ORBI_K6_REPLICAS", request.Replicas.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (request.AllowStress)
            AddEnv(startInfo, "ORBI_K6_ALLOW_STRESS", "true");
        AddEnv(startInfo, "K6_PROMETHEUS_RW_SERVER_URL", "http://prometheus:9090/api/v1/write");
        AddEnv(startInfo, "K6_PROMETHEUS_RW_TREND_STATS", "avg,p(95),p(99)");
        AddEnv(startInfo, "K6_PROMETHEUS_RW_PUSH_INTERVAL", "2s");
        startInfo.ArgumentList.Add("k6");
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--out");
        startInfo.ArgumentList.Add("experimental-prometheus-rw");
        startInfo.ArgumentList.Add("--tag");
        startInfo.ArgumentList.Add($"testid={id}");
        startInfo.ArgumentList.Add("--tag");
        startInfo.ArgumentList.Add($"replicas={request.Replicas}");
        startInfo.ArgumentList.Add($"/work/scenarios/{profile.Script}");

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("No se pudo iniciar el contenedor k6.");
        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await events.PublishAsync(new LabEvent(started, "test", "RUNNING",
            $"Corrida {_safeProfile(request.Profile)} iniciada.", id, request.Replicas, request.Vus),
            CancellationToken.None);
        return new K6RunSession(process, stdout, stderr, stdoutPath, stderrPath, events, commands,
            request, id, started);

        static string _safeProfile(string value) => value;
    }

    public async Task<K6RunResult> ExecuteAsync(K6RunRequest request, string? runId = null, CancellationToken cancellationToken = default)
    {
        var session = await StartAsync(request, runId, cancellationToken);
        await using (session)
            return await session.WaitAsync(sessionDirectory(session.RunId),
                Path.Combine(sessionDirectory(session.RunId), "kubernetes-before.json"),
                Path.Combine(sessionDirectory(session.RunId), "kubernetes-after.json"), cancellationToken);

        string sessionDirectory(string id) => Path.Combine(repositoryRoot, "tests", "k6", "results", id);
    }

    public static string CreateRunId(int replicas, int vus) =>
        $"{DateTimeOffset.UtcNow:yyyy-MM-ddTHHmmssZ}_{replicas}pods_{vus}vus_{Guid.NewGuid():N}"[..Math.Min(80,
            $"{DateTimeOffset.UtcNow:yyyy-MM-ddTHHmmssZ}_{replicas}pods_{vus}vus_{Guid.NewGuid():N}".Length)];

    public static string? ValidateObservedProfile(K6RunRequest request, RunMetrics metrics)
    {
        if (metrics.ObservedVus is null || Math.Abs(metrics.ObservedVus.Value - request.Vus) > 0.5)
            return $"k6 observó {metrics.ObservedVus?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "desconocido"} VUs; se solicitaron {request.Vus}.";
        if (metrics.ObservedDurationSeconds is null ||
            Math.Abs(metrics.ObservedDurationSeconds.Value - request.DurationSeconds) > Math.Max(2, request.DurationSeconds * 0.05))
            return $"k6 observó {metrics.ObservedDurationSeconds?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "duración desconocida"} s; se solicitaron {request.DurationSeconds} s.";
        return null;
    }

    public static RunMetrics ParseMetrics(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;
        var metrics = root.GetProperty("metrics");
        return new RunMetrics(
            ReadValue(metrics, "http_reqs", "count"),
            ReadValue(metrics, "http_reqs", "rate"),
            ReadValue(metrics, "http_req_failed", "rate"),
            ReadValue(metrics, "http_req_duration", "avg"),
            ReadValue(metrics, "http_req_duration", "p(95)"),
            ReadValue(metrics, "http_req_duration", "p(99)"),
            ReadValue(metrics, "checks", "rate"),
            ReadValue(metrics, "vus_max", "max") ?? ReadValue(metrics, "vus", "max"),
            ReadScalar(root, "state", "testRunDurationMs") / 1000d);
    }

    public static bool HasFailedThresholds(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var metrics = json.RootElement.GetProperty("metrics");
        foreach (var metric in metrics.EnumerateObject())
        {
            if (!metric.Value.TryGetProperty("thresholds", out var thresholds))
                continue;
            foreach (var threshold in thresholds.EnumerateObject())
                if (threshold.Value.TryGetProperty("ok", out var ok) && !ok.GetBoolean())
                    return true;
        }
        return false;
    }

    private string SessionDirectory(string id) => Path.Combine(repositoryRoot, "tests", "k6", "results", id);

    private static void AddEnv(ProcessStartInfo startInfo, string key, string value)
    {
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add($"{key}={value}");
    }

    private static double? ReadValue(JsonElement metrics, string metricName, string valueName)
    {
        if (!metrics.TryGetProperty(metricName, out var metric) ||
            !metric.TryGetProperty("values", out var values) ||
            !values.TryGetProperty(valueName, out var value))
            return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;
    }

    private static double? ReadScalar(JsonElement root, string objectName, string propertyName)
    {
        if (!root.TryGetProperty(objectName, out var container) ||
            !container.TryGetProperty(propertyName, out var value))
            return null;
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;
    }
}
