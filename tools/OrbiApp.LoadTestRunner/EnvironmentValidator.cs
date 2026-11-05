using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace OrbiApp.LoadTestRunner;

public sealed record DoctorCheck(string Name, string Status, string Detail);

public sealed class EnvironmentValidator(
    string repositoryRoot,
    CommandRunner commands,
    KubernetesController kubernetes)
{
    private readonly List<DoctorCheck> _checks = [];
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private bool HasFailures => _checks.Any(check => check.Status == "FAIL");

    public async Task<int> RunDoctorAsync(CancellationToken cancellationToken = default)
    {
        _checks.Clear();
        await CheckCommandAsync(".NET SDK", "dotnet", ["--version"], required: true, cancellationToken);
        await CheckCommandAsync("Docker Compose", "docker", ["compose", "version"], required: true, cancellationToken);
        await CheckCommandAsync("Docker Engine", "docker", ["info", "--format", "{{.ServerVersion}}"], required: true, cancellationToken);
        await CheckCommandAsync("kubectl", "kubectl", ["version", "--client", "-o", "json"], required: true, cancellationToken);
        await CheckCommandAsync("Tilt", "tilt", ["version"], required: true, cancellationToken);

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Add("runner environment", string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase) ? "FAIL" : "PASS",
            string.IsNullOrWhiteSpace(environment) ? "no Production" : $"{environment} (runner only)");

        await CheckComposeConfigurationAsync(cancellationToken);
        await CheckKubernetesAsync(cancellationToken);
        await CheckApplicationEndpointsAsync(cancellationToken);
        await CheckMonitoringAsync(cancellationToken);
        CheckHostResources();
        CheckResultDirectory();

        foreach (var check in _checks)
            Console.WriteLine($"{check.Status,-4} {check.Name}: {check.Detail}");
        Console.WriteLine(HasFailures ? "DOCTOR: FAIL; no load suite should start." : "DOCTOR: PASS; warnings remain informational.");
        return HasFailures ? 1 : 0;
    }

    private async Task CheckCommandAsync(string name, string executable, IReadOnlyList<string> args,
        bool required, CancellationToken cancellationToken)
    {
        try
        {
            var result = await commands.RunAsync(executable, args, timeout: TimeSpan.FromSeconds(15), cancellationToken: cancellationToken);
            Add(name, result.ExitCode == 0 ? "PASS" : required ? "FAIL" : "WARN", Compact(result.StandardOutput, result.StandardError));
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Add(name, required ? "FAIL" : "WARN", "herramienta no instalada o fuera de PATH");
        }
    }

    private async Task CheckComposeConfigurationAsync(CancellationToken cancellationToken)
    {
        var compose = ComposeArguments("config", "-q");
        var result = await TryRunAsync("docker", compose, TimeSpan.FromSeconds(30), cancellationToken);
        Add("Compose YAML", result is { ExitCode: 0 } ? "PASS" : "FAIL",
            result is { ExitCode: 0 } ? "configuración válida; salida no mostrada" : Compact(result?.StandardError ?? "Docker Compose no disponible."));

        var services = await TryRunAsync("docker", ComposeArguments("ps", "--services", "--status", "running"), TimeSpan.FromSeconds(15), cancellationToken);
        if (services is null || services.ExitCode != 0)
        {
            Add("PostgreSQL Compose", "FAIL", "no se pudo consultar el servicio; no se inició ni detuvo ningún contenedor");
            return;
        }
        if (!services.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Contains("postgres", StringComparer.Ordinal))
        {
            Add("PostgreSQL Compose", "FAIL", "servicio postgres detenido; Tilt lo iniciaría usando el volumen existente, pero doctor no cambia su estado");
            return;
        }
        var ready = await TryRunAsync("docker", ComposeArguments("exec", "-T", "postgres", "pg_isready", "-q", "-U", "jef_db_first", "-d", "orbi_app"), TimeSpan.FromSeconds(10), cancellationToken);
        Add("PostgreSQL readiness", ready is { ExitCode: 0 } ? "PASS" : "FAIL",
            ready is { ExitCode: 0 } ? "pg_isready correcto (sin conexión ni secreto expuesto)" : "pg_isready no confirmó disponibilidad");
    }

    private async Task CheckKubernetesAsync(CancellationToken cancellationToken)
    {
        var context = await TryRunAsync("kubectl", ["config", "current-context"], TimeSpan.FromSeconds(10), cancellationToken);
        var contextName = context?.StandardOutput.Trim() ?? string.Empty;
        Add("Kubernetes context", context is { ExitCode: 0 } && contextName == "docker-desktop" ? "PASS" : "FAIL",
            string.IsNullOrEmpty(contextName) ? "no se pudo leer el contexto" : contextName);
        if (context is not { ExitCode: 0 } || contextName != "docker-desktop")
            return;

        var kustomize = await TryRunAsync("kubectl", ["kustomize", Path.Combine(repositoryRoot, "infra", "kubernetes", "base")], TimeSpan.FromSeconds(15), cancellationToken);
        Add("Manifiestos Kubernetes", kustomize is { ExitCode: 0 } ? "PASS" : "FAIL",
            kustomize is { ExitCode: 0 } ? "kustomize renderizó los recursos" : Compact(kustomize?.StandardError ?? "kubectl kustomize no disponible"));
        var tiltEval = await TryRunAsync("tilt", ["alpha", "tiltfile-result", "--context", "docker-desktop", "--file", Path.Combine(repositoryRoot, "Tiltfile")], TimeSpan.FromSeconds(20), cancellationToken);
        Add("Tiltfile parse", tiltEval is { ExitCode: 0 } ? "PASS" : "FAIL",
            tiltEval is { ExitCode: 0 } ? "evaluación del Tiltfile correcta; no se desplegaron recursos" : Compact(tiltEval?.StandardError ?? "tilt no disponible"));

        var namespaceResult = await TryRunAsync("kubectl", ["get", "namespace", KubernetesController.Namespace, "-o", "json"], TimeSpan.FromSeconds(15), cancellationToken);
        if (namespaceResult is null || namespaceResult.ExitCode != 0)
        {
            Add("Namespace orbi-lab", "FAIL", "no existe; el doctor no lo crea");
            return;
        }
        var owned = await kubernetes.IsNamespaceOwnedAsync(cancellationToken);
        Add("Namespace ownership", owned ? "PASS" : "FAIL", owned ? "etiqueta de propiedad del laboratorio presente" : "nombre existente sin etiqueta de propiedad; no se adopta");
        if (!owned)
            return;

        try
        {
            var deployment = await kubernetes.GetDeploymentAsync(cancellationToken);
            Add("Orbi Deployment", "PASS", $"{deployment.ReadyReplicas}/{deployment.DesiredReplicas} ready; imagen {deployment.Image}");
            if (deployment.DesiredReplicas > 1)
                Add("Multi-réplica", "WARN", "estado local y workers deben verificarse antes de pruebas de carga");
            var secret = await kubernetes.ReadinessAsync(cancellationToken);
            Add("Orbi Secret", string.IsNullOrWhiteSpace(secret) ? "FAIL" : "PASS", "recurso presente; valores nunca se leen ni se muestran");
        }
        catch (InvalidOperationException exception)
        {
            Add("Orbi Deployment / Secret", "FAIL", exception.Message);
        }

        try
        {
            Add("NodePort 30083", await kubernetes.IsServicePortAvailableAsync(cancellationToken) ? "PASS" : "FAIL",
                "sin colisión global encontrada");
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Text.Json.JsonException)
        {
            Add("NodePort 30083", "FAIL", exception.Message);
        }

        var safe = await kubernetes.IsMultiReplicaApprovedAsync(cancellationToken);
        Add("Guard multi-réplica", safe ? "WARN" : "WARN",
            safe ? "el manifiesto declara revisión aprobada; la aplicación/DB aún requiere prueba" : "false; el runner bloquea réplicas 3 y 10");

        var image = await TryRunAsync("docker", ["image", "inspect", K6Executor.ImageReference, "--format", "{{.Id}}"], TimeSpan.FromSeconds(10), cancellationToken);
        Add("Imagen k6 fijada", image is { ExitCode: 0 } ? "PASS" : "WARN",
            image is { ExitCode: 0 } ? K6Executor.ImageReference : $"{K6Executor.ImageReference} se descargará al ejecutar k6 si la red lo permite");

    }

    private async Task CheckApplicationEndpointsAsync(CancellationToken cancellationToken)
    {
        var url = Environment.GetEnvironmentVariable("ORBI_LAB_BASE_URL") ?? "http://127.0.0.1:30083";
        if (!TryValidateBaseUrl(url, new[] { "127.0.0.1", "localhost" }, out var baseUri, out var issue))
        {
            Add("URL de Orbi", "FAIL", issue ?? "URL local inválida");
            return;
        }
        Add("URL de Orbi", "PASS", baseUri!.ToString().TrimEnd('/'));
        foreach (var path in new[] { "/health/live", "/health/ready", "/api/identity/registration/options" })
        {
            try
            {
                using var response = await _http.GetAsync(new Uri(baseUri!, path), cancellationToken);
                Add($"GET {path}", response.StatusCode == HttpStatusCode.OK ? "PASS" : "FAIL", $"HTTP {(int)response.StatusCode}");
            }
            catch (HttpRequestException)
            {
                Add($"GET {path}", "FAIL", "endpoint local no disponible; solo se intentaron GET");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Add($"GET {path}", "FAIL", "timeout de 5 s");
            }
        }

        var k6Url = Environment.GetEnvironmentVariable("ORBI_LAB_K6_BASE_URL") ?? "http://host.docker.internal:30083";
        Add("URL desde contenedor k6", TryValidateBaseUrl(k6Url, new[] { "host.docker.internal" }, out _, out issue) ? "PASS" : "FAIL",
            issue ?? "host gateway de Docker Desktop; túnel al proxy estable del laboratorio y Service ClusterIP de Orbi");
    }

    private async Task CheckMonitoringAsync(CancellationToken cancellationToken)
    {
        await CheckHttpEndpointAsync("Prometheus readiness", "http://127.0.0.1:19090/-/ready", cancellationToken);
        await CheckHttpEndpointAsync("Grafana API", "http://127.0.0.1:13000/api/health", cancellationToken);
        await CheckHttpEndpointAsync("SignalR event panel", "http://127.0.0.1:15190/health", cancellationToken);
    }

    private async Task CheckHttpEndpointAsync(string name, string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            Add(name, response.IsSuccessStatusCode ? "PASS" : "WARN", $"HTTP {(int)response.StatusCode}; localhost only");
        }
        catch (HttpRequestException)
        {
            Add(name, "WARN", "no iniciado; tilt up lo preparará sin iniciar pruebas k6");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Add(name, "WARN", "timeout al consultar loopback");
        }
    }

    private void CheckHostResources()
    {
        var root = Path.GetPathRoot(repositoryRoot);
        var free = !string.IsNullOrEmpty(root) && DriveInfo.GetDrives().FirstOrDefault(drive => drive.Name.Equals(root, StringComparison.OrdinalIgnoreCase)) is { IsReady: true } driveInfo
            ? driveInfo.AvailableFreeSpace
            : -1;
        Add("Host recursos", "PASS", $"CPU={Environment.ProcessorCount}; RAM disponible≈{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024} MiB; espacio={free / 1024 / 1024} MiB");
        Add("Reloj UTC", "WARN", $"{DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)}; sincronización NTP no comprobada");
        CheckPort("Prometheus host port 19090", 19090, "Prometheus readiness");
        CheckPort("Grafana host port 13000", 13000, "Grafana API");
        CheckPort("Events host port 15190", 15190, "SignalR event panel");
    }

    private void CheckResultDirectory()
    {
        try
        {
            var path = Path.Combine(repositoryRoot, "tests", "k6", "results");
            Directory.CreateDirectory(path);
            var check = Path.Combine(path, $".write-check-{Guid.NewGuid():N}");
            File.WriteAllText(check, string.Empty);
            File.Delete(check);
            Add("Resultados escribibles", "PASS", "tests/k6/results");
        }
        catch (IOException)
        {
            Add("Resultados escribibles", "FAIL", "no se pudo crear o limpiar una comprobación temporal");
        }
        catch (UnauthorizedAccessException)
        {
            Add("Resultados escribibles", "FAIL", "acceso denegado");
        }
    }

    private void CheckPort(string name, int port, string expectedEndpoint)
    {
        if (_checks.Any(check => check.Name == expectedEndpoint && check.Status == "PASS"))
        {
            Add(name, "PASS", "el endpoint del servicio de laboratorio respondió correctamente");
            return;
        }

        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            Add(name, "PASS", "disponible en loopback");
        }
        catch (SocketException)
        {
            Add(name, "FAIL", "el puerto local ya está en uso");
        }
    }

    private async Task<CommandResult?> TryRunAsync(string executable, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try { return await commands.RunAsync(executable, args, timeout: timeout, cancellationToken: cancellationToken); }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    private IReadOnlyList<string> ComposeArguments(params string[] args)
    {
        var arguments = new List<string> { "compose", "-p", "orbi-app", "-f", Path.Combine(repositoryRoot, "docker-compose.yml"),
            "-f", Path.Combine(repositoryRoot, "docker-compose.monitoring.yml"), "-f", Path.Combine(repositoryRoot, "docker-compose.k6.yml") };
        arguments.AddRange(args);
        return arguments;
    }

    private static bool TryValidateBaseUrl(string input, IReadOnlyCollection<string> allowedHosts, out Uri? uri, out string? issue)
    {
        uri = null;
        issue = null;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttp ||
            parsed.Port != KubernetesController.NodePort || parsed.UserInfo.Length > 0 || parsed.Query.Length > 0 || parsed.Fragment.Length > 0 ||
            parsed.AbsolutePath != "/" || !allowedHosts.Contains(parsed.Host, StringComparer.OrdinalIgnoreCase))
        {
            issue = "la URL debe usar HTTP y el host permitido en el NodePort local 30083, sin credenciales ni ruta";
            return false;
        }
        uri = parsed;
        return true;
    }

    private void Add(string name, string status, string detail) => _checks.Add(new DoctorCheck(name, status, detail));

    private static string Compact(params string[] values)
    {
        var value = string.Join(" ", values.Where(item => !string.IsNullOrWhiteSpace(item))).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return value.Length <= 250 ? value : value[..250];
    }
}
