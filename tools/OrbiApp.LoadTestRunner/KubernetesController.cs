using System.Text.Json;

namespace OrbiApp.LoadTestRunner;

public sealed record DeploymentSnapshot(
    string Uid,
    string Name,
    int DesiredReplicas,
    int ReadyReplicas,
    int AvailableReplicas,
    string Image,
    string Environment,
    string TemplateFingerprint);

public sealed class KubernetesController(CommandRunner runner)
{
    public const string Namespace = "orbi-lab";
    public const string DeploymentName = "orbi";
    public const int NodePort = 30083;
    private const string OwnerLabel = "app.kubernetes.io/part-of";
    private const string OwnerValue = "orbi-load-test-lab";

    public async Task<string> CurrentContextAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(["config", "current-context"], cancellationToken: cancellationToken);
        EnsureSuccess(result, "consultar el contexto Kubernetes");
        return result.StandardOutput.Trim();
    }

    public async Task EnsureLocalContextAsync(CancellationToken cancellationToken = default)
    {
        var context = await CurrentContextAsync(cancellationToken);
        if (!string.Equals(context, "docker-desktop", StringComparison.Ordinal))
            throw new InvalidOperationException($"Contexto bloqueado: '{context}'. Solo se permite docker-desktop.");
    }

    public async Task<DeploymentSnapshot> GetDeploymentAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(
            ["get", "deployment", DeploymentName, "-n", Namespace, "-o", "json"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result, "leer el Deployment del laboratorio");
        using var json = JsonDocument.Parse(result.StandardOutput);
        var root = json.RootElement;
        EnsureOwner(root.GetProperty("metadata"));
        var spec = root.GetProperty("spec");
        var status = root.TryGetProperty("status", out var s) ? s : default;
        var template = spec.GetProperty("template");
        var podLabels = template.GetProperty("metadata").GetProperty("labels");
        if (!podLabels.TryGetProperty("environment", out var environmentLabel) || environmentLabel.GetString() != "lab")
            throw new InvalidOperationException("El selector de Pods no identifica el entorno lab; se cancela por seguridad.");

        var containers = template.GetProperty("spec").GetProperty("containers");
        var appContainer = containers.EnumerateArray().FirstOrDefault(container => container.GetProperty("name").GetString() == "orbi");
        if (appContainer.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("No se encontró el contenedor esperado 'orbi'.");

        var environment = appContainer.TryGetProperty("env", out var envVars)
            ? envVars.EnumerateArray()
                .Where(item => item.GetProperty("name").GetString() == "ASPNETCORE_ENVIRONMENT")
                .Select(item => item.TryGetProperty("value", out var value) ? value.GetString() : null)
                .FirstOrDefault()
            : null;
        if (!string.Equals(environment, "Lab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El Deployment debe usar ASPNETCORE_ENVIRONMENT=Lab.");

        return new DeploymentSnapshot(
            root.GetProperty("metadata").GetProperty("uid").GetString() ?? string.Empty,
            DeploymentName,
            spec.TryGetProperty("replicas", out var desired) ? desired.GetInt32() : 1,
            status.ValueKind == JsonValueKind.Object && status.TryGetProperty("readyReplicas", out var ready) ? ready.GetInt32() : 0,
            status.ValueKind == JsonValueKind.Object && status.TryGetProperty("availableReplicas", out var available) ? available.GetInt32() : 0,
            appContainer.GetProperty("image").GetString() ?? string.Empty,
            environment!,
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(template.GetRawText()))));
    }

    public async Task<string> GetPodsJsonAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(
            ["get", "pods", "-n", Namespace, "-l", "app.kubernetes.io/name=orbi", "-o", "json"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result, "leer Pods del laboratorio");
        using var json = JsonDocument.Parse(result.StandardOutput);
        var items = json.RootElement.GetProperty("items");
        foreach (var pod in items.EnumerateArray())
            EnsureOwner(pod.GetProperty("metadata"));
        return result.StandardOutput;
    }

    public async Task<string> GetDeploymentJsonAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(
            ["get", "deployment", DeploymentName, "-n", Namespace, "-o", "json"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result, "capturar el Deployment del laboratorio");
        using var json = JsonDocument.Parse(result.StandardOutput);
        EnsureOwner(json.RootElement.GetProperty("metadata"));
        return result.StandardOutput;
    }

    public async Task<bool> IsNamespaceOwnedAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(["get", "namespace", Namespace, "-o", "json"], cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            if (result.StandardError.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
                return false;
            throw new InvalidOperationException("No se pudo inspeccionar el namespace orbi-lab.");
        }
        using var json = JsonDocument.Parse(result.StandardOutput);
        return HasOwner(json.RootElement.GetProperty("metadata"));
    }

    public async Task EnsureNamespaceSafeForApplyAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(["get", "namespace", Namespace, "-o", "json"], cancellationToken: cancellationToken);
        if (result.ExitCode != 0 && result.StandardError.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
        {
            var manifest = System.Text.Json.JsonSerializer.Serialize(new
            {
                apiVersion = "v1",
                kind = "Namespace",
                metadata = new { name = Namespace, labels = new Dictionary<string, string> { [OwnerLabel] = OwnerValue, ["environment"] = "lab" } },
            });
            var created = await KubectlAsync(["create", "-f", "-"], standardInput: manifest, cancellationToken: cancellationToken);
            if (created.ExitCode == 0)
                return;
            result = await KubectlAsync(["get", "namespace", Namespace, "-o", "json"], cancellationToken: cancellationToken);
        }
        EnsureSuccess(result, "validar el namespace antes de Tilt apply");
        using var json = JsonDocument.Parse(result.StandardOutput);
        EnsureOwner(json.RootElement.GetProperty("metadata"));
    }

    public async Task<bool> IsMultiReplicaApprovedAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(
            ["get", "configmap", "orbi-lab-config", "-n", Namespace, "-o", "json"],
            cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
            return false;
        using var json = JsonDocument.Parse(result.StandardOutput);
        EnsureOwner(json.RootElement.GetProperty("metadata"));
        var data = json.RootElement.GetProperty("data");
        return data.TryGetProperty("ORBI_LAB_MULTI_REPLICA_SAFE", out var value) && value.GetString() == "true";
    }

    public async Task<bool> IsServicePortAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(["get", "services", "-A", "-o", "json"], cancellationToken: cancellationToken);
        EnsureSuccess(result, "comprobar colisiones de NodePort");
        using var json = JsonDocument.Parse(result.StandardOutput);
        foreach (var service in json.RootElement.GetProperty("items").EnumerateArray())
        {
            var metadata = service.GetProperty("metadata");
            var sameLabService = metadata.GetProperty("namespace").GetString() == Namespace &&
                metadata.GetProperty("name").GetString() == "orbi";
            if (!service.TryGetProperty("spec", out var spec) || !spec.TryGetProperty("ports", out var ports))
                continue;
            if (ports.EnumerateArray().Any(port => port.TryGetProperty("nodePort", out var nodePort) && nodePort.GetInt32() == NodePort))
                return sameLabService;
        }
        return true;
    }

    public async Task<string> ReadinessAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(
            ["get", "secret", "orbi-app-secrets", "-n", Namespace, "-o", "json"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result, "comprobar Secret de laboratorio");
        using var json = JsonDocument.Parse(result.StandardOutput);
        var metadata = json.RootElement.GetProperty("metadata");
        EnsureOwner(metadata);
        var labels = metadata.GetProperty("labels");
        if (!labels.TryGetProperty("environment", out var environment) || environment.GetString() != "lab")
            throw new InvalidOperationException("El Secret no está identificado como secreto de laboratorio.");
        return "orbi-app-secrets presente y etiquetado; valores omitidos";
    }

    public async Task<CommandResult> ScaleAsync(int replicas, CancellationToken cancellationToken = default)
    {
        if (replicas is not (1 or 3 or 10))
            throw new ArgumentOutOfRangeException(nameof(replicas));
        return await KubectlAsync(
            ["scale", $"deployment/{DeploymentName}", $"--replicas={replicas}", "-n", Namespace],
            cancellationToken: cancellationToken);
    }

    public async Task<CommandResult> WaitRolloutAsync(TimeSpan timeout, CancellationToken cancellationToken = default) =>
        await KubectlAsync(
            ["rollout", "status", $"deployment/{DeploymentName}", "-n", Namespace, $"--timeout={Math.Max(1, (int)timeout.TotalSeconds)}s"],
            timeout: timeout + TimeSpan.FromSeconds(5),
            cancellationToken: cancellationToken);

    public async Task<CommandResult> SetImageAsync(string image, CancellationToken cancellationToken = default) =>
        await KubectlAsync(
            ["set", "image", $"deployment/{DeploymentName}", $"orbi={image}", "-n", Namespace],
            cancellationToken: cancellationToken);

    public async Task<CommandResult> RollbackAsync(CancellationToken cancellationToken = default) =>
        await KubectlAsync(["rollout", "undo", $"deployment/{DeploymentName}", "-n", Namespace], cancellationToken: cancellationToken);

    public async Task<string> GetEventSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var result = await KubectlAsync(
            ["get", "pods", "-n", Namespace, "-l", "app.kubernetes.io/name=orbi", "-o", "json"],
            cancellationToken: cancellationToken);
        EnsureSuccess(result, "capturar Pods del laboratorio");
        using var json = JsonDocument.Parse(result.StandardOutput);
        foreach (var pod in json.RootElement.GetProperty("items").EnumerateArray())
            EnsureOwner(pod.GetProperty("metadata"));
        return result.StandardOutput;
    }

    public async Task<CommandResult> DeletePodAsync(string podName, CancellationToken cancellationToken = default)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(podName, "^orbi-[a-z0-9-]+$"))
            throw new InvalidOperationException("Nombre de Pod no admitido.");
        var pod = await KubectlAsync(["get", "pod", podName, "-n", Namespace, "-o", "json"], cancellationToken: cancellationToken);
        EnsureSuccess(pod, "verificar el Pod objetivo");
        using (var json = JsonDocument.Parse(pod.StandardOutput))
        {
            var root = json.RootElement;
            var metadata = root.GetProperty("metadata");
            EnsureOwner(metadata);
            var labels = metadata.GetProperty("labels");
            if (!labels.TryGetProperty("environment", out var environment) || environment.GetString() != "lab" ||
                !labels.TryGetProperty("app.kubernetes.io/name", out var app) || app.GetString() != "orbi")
                throw new InvalidOperationException("El Pod no está identificado como instancia de Orbi Lab.");
        }
        return await KubectlAsync(
            ["delete", "pod", podName, "-n", Namespace, "--wait=false"],
            cancellationToken: cancellationToken);
    }

    public async Task<CommandResult> RunKubectlAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default) =>
        await KubectlAsync(args, cancellationToken: cancellationToken);

    public async Task<CommandResult> ApplySecretAsync(string jsonManifest, CancellationToken cancellationToken = default) =>
        await KubectlAsync(["apply", "-n", Namespace, "-f", "-"], standardInput: jsonManifest, cancellationToken: cancellationToken);

    private async Task<CommandResult> KubectlAsync(
        IReadOnlyList<string> args,
        string? standardInput = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        await runner.RunAsync("kubectl", args, standardInput: standardInput,
            timeout: timeout ?? TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);

    private static bool HasOwner(JsonElement metadata) => metadata.TryGetProperty("labels", out var labels) &&
        labels.TryGetProperty(OwnerLabel, out var owner) && owner.GetString() == OwnerValue;

    private static void EnsureOwner(JsonElement metadata)
    {
        if (!HasOwner(metadata))
            throw new InvalidOperationException("El recurso no lleva la etiqueta de propiedad del laboratorio Orbi; se cancela por seguridad.");
    }

    private static void EnsureSuccess(CommandResult result, string action)
    {
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"No se pudo {action}. {SummarizeError(result.StandardError)}");
    }

    private static string SummarizeError(string error)
    {
        var safe = error.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return safe.Length <= 300 ? safe : safe[..300];
    }
}
