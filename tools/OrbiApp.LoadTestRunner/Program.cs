using System.Diagnostics;
using System.Text.Json;

namespace OrbiApp.LoadTestRunner;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        try
        {
            var options = RunnerOptions.Parse(args);
            if (options.Command == "help") { PrintHelp(); return 0; }
            var root = LabPaths.FindRepositoryRoot();
            var commandRunner = new CommandRunner();
            var kubernetes = new KubernetesController(commandRunner);
            var events = new EventPublisher();
            var reports = new ReportWriter(root);
            switch (options.Command)
            {
                case "doctor":
                    return await new EnvironmentValidator(root, commandRunner, kubernetes).RunDoctorAsync(cancellation.Token);
                case "events":
                    await EventsServer.RunAsync(cancellation.Token); return 0;
                case "preflight":
                    await kubernetes.EnsureLocalContextAsync(cancellation.Token);
                    await kubernetes.EnsureNamespaceSafeForApplyAsync(cancellation.Token);
                    if (!await kubernetes.IsServicePortAvailableAsync(cancellation.Token))
                        throw new InvalidOperationException("NodePort 30083 está ocupado por un Service ajeno al laboratorio.");
                    Console.WriteLine("Preflight local correcto; Tilt puede aplicar únicamente los recursos orbi-lab previstos."); return 0;
                case "lab-secret":
                    await new LabSecretManager(root, kubernetes).EnsureSecretAsync(cancellation.Token); return 0;
                case "report":
                    await reports.RefreshComparisonAsync(cancellation.Token); Console.WriteLine("Reporte actualizado en tests/k6/results/comparison.csv y comparison.md."); return 0;
                case "smoke":
                    return await RunOneAsync(root, commandRunner, kubernetes, events, reports,
                        new K6RunRequest("smoke", 1, 10, 1, false), cancellation.Token);
                case "baseline":
                    return await RunOneAsync(root, commandRunner, kubernetes, events, reports,
                        new K6RunRequest("baseline", 100, 30, 1, false), cancellation.Token);
                case "matrix":
                    return await RunMatrixAsync(root, commandRunner, kubernetes, events, reports, options, cancellation.Token);
                case "availability":
                    return await RunAvailabilityAsync(root, commandRunner, kubernetes, events, reports, cancellation.Token);
                case "rollout":
                    return await RunRolloutAsync(root, commandRunner, kubernetes, events, reports, options, cancellation.Token);
                default: throw new ArgumentException($"Comando no reconocido: {options.Command}");
            }
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Operación cancelada; los procesos hijos se detuvieron."); return 130; }
        catch (Exception exception) { Console.Error.WriteLine($"ERROR: {Sanitize(exception.Message)}"); return 1; }
    }

    private static async Task<int> RunOneAsync(string root, CommandRunner commands, KubernetesController kube,
        EventPublisher events, ReportWriter reports, K6RunRequest request, CancellationToken token)
    {
        await EnsureLabAsync(kube, request.Replicas, token);
        var before = await kube.GetDeploymentJsonAsync(token);
        var podsBefore = await kube.GetPodsJsonAsync(token);
        var runId = K6Executor.CreateRunId(request.Replicas, request.Vus);
        var runDir = Path.Combine(root, "tests", "k6", "results", runId);
        Directory.CreateDirectory(runDir);
        var beforePath = Path.Combine(runDir, "kubernetes-before.json");
        var afterPath = Path.Combine(runDir, "kubernetes-after.json");
        await File.WriteAllTextAsync(beforePath, JsonSerializer.Serialize(new { deployment = JsonDocument.Parse(before).RootElement,
            pods = JsonDocument.Parse(podsBefore).RootElement }), token);
        var executor = new K6Executor(root, commands, events);
        var run = await executor.ExecuteAsync(request, runId, token);
        var after = await kube.GetDeploymentJsonAsync(token);
        var podsAfter = await kube.GetPodsJsonAsync(token);
        await File.WriteAllTextAsync(afterPath, JsonSerializer.Serialize(new { deployment = JsonDocument.Parse(after).RootElement,
            pods = JsonDocument.Parse(podsAfter).RootElement }), token);
        var snapshot = await kube.GetDeploymentAsync(token);
        var metadata = new RunMetadata
        {
            RunId = run.RunId, Profile = request.Profile, StartedAtUtc = run.StartedAtUtc,
            GitCommit = await GitCommitAsync(commands, root, token), AppImage = snapshot.Image,
            ReplicasRequested = request.Replicas, ReplicasReady = snapshot.ReadyReplicas,
            Vus = request.Vus, VusObserved = run.Metrics?.ObservedVus,
            DurationSeconds = request.DurationSeconds, DurationObservedSeconds = run.Metrics?.ObservedDurationSeconds, Environment = "Lab",
            OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ProcessorCount = Environment.ProcessorCount, RunStatus = run.Status,
            ThresholdStatus = run.Status == "PASS" ? "PASS" : run.Status == "THRESHOLD_FAIL" ? "FAIL" : "UNKNOWN",
            ExitCode = run.ExitCode, ToolVersions = await ToolVersionsAsync(commands, token),
            K6Image = K6Executor.ImageReference, Notes = run.Notes
        };
        await reports.SaveRunAsync(metadata, token);
        Console.WriteLine($"{run.Status}: {run.RunId}; resultados: {run.Directory}");
        return run.ExitCode;
    }

    private static async Task<int> RunMatrixAsync(string root, CommandRunner commands, KubernetesController kube,
        EventPublisher events, ReportWriter reports, RunnerOptions options, CancellationToken token)
    {
        await reports.RefreshComparisonAsync(token);
        await EnsureLabAsync(kube, 1, token);
        if (!await kube.IsMultiReplicaApprovedAsync(token))
            throw new InvalidOperationException("Matriz bloqueada: revisar Data Protection, workers, inicialización/migraciones y persistencia; el ConfigMap del lab indica multi-réplica no segura.");
        if (!options.ConfirmMultiReplica)
            throw new InvalidOperationException("La matriz requiere --confirm-multi-replica tras aprobar la evaluación de seguridad.");
        var original = await kube.GetDeploymentAsync(token);
        var ownerUid = original.Uid;
        var originalTemplate = original.TemplateFingerprint;
        var lastAppliedReplicas = original.DesiredReplicas;
        var result = 0;
        try
        {
            foreach (var group in MatrixPlanner.Plan(options.MaxVus).GroupBy(item => item.Replicas))
            {
                if (group.Key != original.DesiredReplicas)
                {
                    await kube.ScaleAsync(group.Key, token);
                    lastAppliedReplicas = group.Key;
                    await events.PublishAsync(new LabEvent(DateTimeOffset.UtcNow, "replicas", "SCALING",
                        $"Escalando Deployment orbi a {group.Key} réplicas.", null, group.Key), token);
                    var rollout = await kube.WaitRolloutAsync(TimeSpan.FromMinutes(5), token);
                    if (rollout.ExitCode != 0) throw new InvalidOperationException("Timeout esperando Pods Ready durante escalado.");
                }
                var ready = await kube.GetDeploymentAsync(token);
                if (ready.Uid != ownerUid || ready.TemplateFingerprint != originalTemplate || ready.ReadyReplicas != group.Key)
                    throw new InvalidOperationException("Deployment cambió o no alcanzó todas las réplicas Ready; se detiene la matriz.");
                foreach (var item in group)
                {
                    var code = await RunOneAsync(root, commands, kube, events, reports,
                        new K6RunRequest("scalability", item.Vus, item.DurationSeconds, item.Replicas, options.AllowStress), token);
                    if (code != 0) { result = code; if (item.Vus <= 100) return result; }
                }
            }
            return result;
        }
        finally
        {
            var current = await kube.GetDeploymentAsync(CancellationToken.None);
            if (current.Uid == ownerUid && current.TemplateFingerprint == originalTemplate && current.Image == original.Image &&
                current.DesiredReplicas == lastAppliedReplicas && current.DesiredReplicas != original.DesiredReplicas)
            {
                await kube.ScaleAsync(original.DesiredReplicas, CancellationToken.None);
                await kube.WaitRolloutAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
            }
        }
    }

    private static async Task<int> RunRolloutAsync(string root, CommandRunner commands, KubernetesController kube,
        EventPublisher events, ReportWriter reports, RunnerOptions options, CancellationToken token)
    {
        await EnsureLabAsync(kube, 1, token);
        if (!await kube.IsMultiReplicaApprovedAsync(token)) throw new InvalidOperationException("Rollout bloqueado: multi-réplica no está aprobada.");
        ValidateImmutableTag(options.FromImage!); ValidateImmutableTag(options.ToImage!);
        var original = await kube.GetDeploymentAsync(token);
        if (original.DesiredReplicas > 1 && !await kube.IsMultiReplicaApprovedAsync(token))
            throw new InvalidOperationException("El Deployment usa más de una réplica sin autorización de seguridad.");
        if (original.DesiredReplicas < 2)
            throw new InvalidOperationException("rollout requiere al menos dos réplicas para evaluar actualización con tráfico.");
        if (original.Image != options.FromImage) throw new InvalidOperationException("La imagen actual no coincide con --from; no se modifica el Deployment.");
        foreach (var candidateImage in new[] { options.FromImage!, options.ToImage! })
        {
            var inspected = await commands.RunAsync("docker", ["image", "inspect", candidateImage, "--format", "{{.Id}}"],
                timeout: TimeSpan.FromSeconds(15), cancellationToken: token);
            if (inspected.ExitCode != 0) throw new InvalidOperationException($"La imagen versionada no está disponible localmente: {candidateImage}.");
        }
        var image = await kube.RunKubectlAsync(["get", "deployment", "orbi", "-n", KubernetesController.Namespace, "-o", "jsonpath={.metadata.uid}"], token);
        if (image.ExitCode != 0) throw new InvalidOperationException("No se pudo verificar el Deployment del laboratorio.");
        var runId = K6Executor.CreateRunId(original.DesiredReplicas, 20);
        var directory = Path.Combine(root, "tests", "k6", "results", runId);
        Directory.CreateDirectory(directory);
        var beforePath = Path.Combine(directory, "kubernetes-before.json");
        var afterPath = Path.Combine(directory, "kubernetes-after.json");
        await SaveSnapshotAsync(kube, beforePath, token);
        var executor = new K6Executor(root, commands, events);
        var startedAt = DateTimeOffset.UtcNow;
        var session = await executor.StartAsync(new K6RunRequest("rollout", 20, 90, original.DesiredReplicas, false), runId, token);
        try { await new PrometheusClient().WaitForRunRequestsAsync(runId, TimeSpan.FromSeconds(30), token); }
        catch { await session.CancelAsync(); await session.DisposeAsync(); throw; }
        var set = await kube.SetImageAsync(options.ToImage!, token);
        if (set.ExitCode != 0) { await session.CancelAsync(); await session.DisposeAsync(); throw new InvalidOperationException("kubectl set image falló."); }
        var expectedRollout = await kube.GetDeploymentAsync(token);
        if (expectedRollout.Uid != original.Uid || expectedRollout.Image != options.ToImage)
        {
            await session.CancelAsync(); await session.DisposeAsync();
            throw new InvalidOperationException("El Deployment cambió antes de iniciar el rollout esperado; no se continúa.");
        }
        await events.PublishAsync(new LabEvent(DateTimeOffset.UtcNow, "rollout", "RUNNING", "Rolling Update iniciado con tráfico k6 activo.", runId, original.DesiredReplicas, 20, options.ToImage), token);
        var rollout = await kube.WaitRolloutAsync(TimeSpan.FromMinutes(5), token);
        var current = await kube.GetDeploymentAsync(token);
        var uidAfter = await kube.RunKubectlAsync(["get", "deployment", "orbi", "-n", KubernetesController.Namespace, "-o", "jsonpath={.metadata.uid}"], token);
        var status = rollout.ExitCode == 0 ? "PASS" : "INFRA_ERROR";
        if (rollout.ExitCode != 0)
        {
            if (current.Uid == original.Uid && uidAfter.StandardOutput == image.StandardOutput &&
                current.Image == options.ToImage && current.TemplateFingerprint == expectedRollout.TemplateFingerprint)
            {
                var undo = await kube.RollbackAsync(token);
                if (undo.ExitCode == 0) await kube.WaitRolloutAsync(TimeSpan.FromMinutes(5), token);
            }
            await events.PublishAsync(new LabEvent(DateTimeOffset.UtcNow, "rollout", "FAIL", "Rolling Update falló; rollback condicionado a que el Deployment conserve UID e imagen del ensayo.", runId, original.DesiredReplicas, 20), token);
        }
        try
        {
            var result = await session.WaitAsync(directory, beforePath, afterPath, token);
            await SaveSnapshotAsync(kube, afterPath, token);
            var finalDeployment = await kube.GetDeploymentAsync(token);
            var runStatus = status == "PASS" && result.ExitCode == 0 ? "PASS" : result.Status == "THRESHOLD_FAIL" ? "THRESHOLD_FAIL" : "INFRA_ERROR";
            var metadata = new RunMetadata
            {
                RunId = runId, Profile = "rollout", StartedAtUtc = startedAt,
                GitCommit = await GitCommitAsync(commands, root, token), AppImage = finalDeployment.Image,
                ReplicasRequested = original.DesiredReplicas, ReplicasReady = finalDeployment.ReadyReplicas,
                Vus = 20, VusObserved = result.Metrics?.ObservedVus,
                DurationSeconds = 90, DurationObservedSeconds = result.Metrics?.ObservedDurationSeconds, Environment = "Lab",
                OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                ProcessorCount = Environment.ProcessorCount, RunStatus = runStatus,
                ThresholdStatus = runStatus == "PASS" ? "PASS" : runStatus == "THRESHOLD_FAIL" ? "FAIL" : "UNKNOWN",
                ExitCode = runStatus == "PASS" ? 0 : Math.Max(1, result.ExitCode),
                ToolVersions = await ToolVersionsAsync(commands, token), K6Image = K6Executor.ImageReference,
                Notes = $"from={options.FromImage}; to={options.ToImage}; {result.Notes}"
            };
            await reports.SaveRunAsync(metadata, token);
            await events.PublishAsync(new LabEvent(DateTimeOffset.UtcNow, "rollout", runStatus,
                runStatus == "PASS" ? "Rolling Update completado con readiness confirmada." : "Rolling Update registrado como fallido.",
                runId, original.DesiredReplicas, 20, finalDeployment.Image), token);
            return metadata.ExitCode;
        }
        finally { await session.DisposeAsync(); }
    }

    private static async Task<int> RunAvailabilityAsync(string root, CommandRunner commands, KubernetesController kube,
        EventPublisher events, ReportWriter reports, CancellationToken token)
    {
        await EnsureLabAsync(kube, 2, token);
        if (!await kube.IsMultiReplicaApprovedAsync(token))
            throw new InvalidOperationException("availability bloqueado: el laboratorio no tiene aprobada la seguridad multi-réplica.");
        var deployment = await kube.GetDeploymentAsync(token);
        if (deployment.DesiredReplicas < 2 || deployment.ReadyReplicas < 2)
            throw new InvalidOperationException("availability requiere al menos dos Pods Ready antes de iniciar.");
        using var podsJson = JsonDocument.Parse(await kube.GetPodsJsonAsync(token));
        var target = podsJson.RootElement.GetProperty("items").EnumerateArray()
            .Where(pod => pod.GetProperty("status").TryGetProperty("phase", out var phase) && phase.GetString() == "Running")
            .FirstOrDefault(pod => pod.GetProperty("status").TryGetProperty("conditions", out var conditions) &&
                conditions.EnumerateArray().Any(condition => condition.GetProperty("type").GetString() == "Ready" && condition.GetProperty("status").GetString() == "True"));
        if (target.ValueKind == JsonValueKind.Undefined) throw new InvalidOperationException("No hay Pod Ready verificable para la prueba.");
        var targetName = target.GetProperty("metadata").GetProperty("name").GetString()!;
        var id = K6Executor.CreateRunId(deployment.DesiredReplicas, 20);
        var directory = Path.Combine(root, "tests", "k6", "results", id);
        Directory.CreateDirectory(directory);
        var beforePath = Path.Combine(directory, "kubernetes-before.json");
        var afterPath = Path.Combine(directory, "kubernetes-after.json");
        await SaveSnapshotAsync(kube, beforePath, token);
        var executor = new K6Executor(root, commands, events);
        var startedAt = DateTimeOffset.UtcNow;
        var session = await executor.StartAsync(new K6RunRequest("availability", 20, 90, deployment.DesiredReplicas, false), id, token);
        double? recovery = null;
        try
        {
            await new PrometheusClient().WaitForRunRequestsAsync(id, TimeSpan.FromSeconds(30), token);
            var delete = await kube.DeletePodAsync(targetName, token);
            if (delete.ExitCode != 0) throw new InvalidOperationException("No se pudo eliminar el Pod de prueba del namespace orbi-lab.");
            await events.PublishAsync(new LabEvent(DateTimeOffset.UtcNow, "pod", "DELETED", "Pod de laboratorio eliminado durante disponibilidad.", id, deployment.DesiredReplicas, 20), token);
            var recoveryStart = DateTimeOffset.UtcNow;
            while (DateTimeOffset.UtcNow - recoveryStart < TimeSpan.FromMinutes(3))
            {
                token.ThrowIfCancellationRequested();
                var current = await kube.GetDeploymentAsync(token);
                if (current.Uid != deployment.Uid) throw new InvalidOperationException("Deployment cambió durante la prueba; se detiene.");
                if (current.ReadyReplicas >= deployment.DesiredReplicas) { recovery = (DateTimeOffset.UtcNow - recoveryStart).TotalSeconds; break; }
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }
            if (recovery is null) throw new TimeoutException("No se recuperaron todas las réplicas Ready en tres minutos.");
            await events.PublishAsync(new LabEvent(DateTimeOffset.UtcNow, "pod", "RECOVERED",
                $"Readiness recuperada en {recovery.Value:0.###} s.", id, deployment.DesiredReplicas, 20), token);
            var result = await session.WaitAsync(directory, beforePath, afterPath, token);
            await SaveSnapshotAsync(kube, afterPath, token);
            var currentDeployment = await kube.GetDeploymentAsync(token);
            var metadata = new RunMetadata
            {
                RunId = id, Profile = "availability", StartedAtUtc = startedAt,
                GitCommit = await GitCommitAsync(commands, root, token), AppImage = currentDeployment.Image,
                ReplicasRequested = deployment.DesiredReplicas, ReplicasReady = currentDeployment.ReadyReplicas,
                Vus = 20, VusObserved = result.Metrics?.ObservedVus,
                DurationSeconds = 90, DurationObservedSeconds = result.Metrics?.ObservedDurationSeconds, Environment = "Lab",
                OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                ProcessorCount = Environment.ProcessorCount, RunStatus = result.Status,
                ThresholdStatus = result.Status == "PASS" ? "PASS" : result.Status == "THRESHOLD_FAIL" ? "FAIL" : "UNKNOWN",
                ExitCode = result.ExitCode, ToolVersions = await ToolVersionsAsync(commands, token),
                K6Image = K6Executor.ImageReference, RecoveryPod = targetName, RecoverySeconds = recovery,
                Notes = result.Notes
            };
            await reports.SaveRunAsync(metadata, token);
            return result.ExitCode;
        }
        catch
        {
            await session.CancelAsync();
            throw;
        }
        finally { await session.DisposeAsync(); }
    }

    private static async Task SaveSnapshotAsync(KubernetesController kube, string path, CancellationToken token)
    {
        using var deployment = JsonDocument.Parse(await kube.GetDeploymentJsonAsync(token));
        using var pods = JsonDocument.Parse(await kube.GetPodsJsonAsync(token));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { deployment = deployment.RootElement, pods = pods.RootElement }), token);
    }

    private static async Task EnsureLabAsync(KubernetesController kube, int replicas, CancellationToken token)
    {
        await kube.EnsureLocalContextAsync(token);
        if (!await kube.IsNamespaceOwnedAsync(token)) throw new InvalidOperationException("Namespace orbi-lab no existe o no está etiquetado como propiedad de este laboratorio.");
        if (!await kube.IsServicePortAvailableAsync(token)) throw new InvalidOperationException("NodePort 30083 está ocupado por otro Service.");
        await kube.ReadinessAsync(token);
        var snapshot = await kube.GetDeploymentAsync(token);
        if (snapshot.Environment != "Lab") throw new InvalidOperationException("Deployment no es Environment=Lab.");
        if (snapshot.ReadyReplicas != snapshot.DesiredReplicas || snapshot.DesiredReplicas < replicas)
            throw new InvalidOperationException("El Deployment no tiene todas las réplicas Ready requeridas.");
        if (replicas > 1 && !await kube.IsMultiReplicaApprovedAsync(token)) throw new InvalidOperationException("Escalado multi-réplica bloqueado por seguridad.");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        var appUrl = Environment.GetEnvironmentVariable("ORBI_LAB_BASE_URL") ?? "http://127.0.0.1:30083";
        if (!Uri.TryCreate(appUrl, UriKind.Absolute, out var appBase) || appBase.Scheme != "http" ||
            appBase.Port != KubernetesController.NodePort || appBase.UserInfo.Length != 0 ||
            appBase.Host is not ("127.0.0.1" or "localhost") || appBase.AbsolutePath != "/" || appBase.Query.Length > 0 || appBase.Fragment.Length > 0)
            throw new InvalidOperationException("ORBI_LAB_BASE_URL debe ser loopback HTTP en el NodePort local 30083.");
        await EnsureHttpOkAsync(http, new Uri(appBase, "/health/ready"), "Orbi /health/ready", token);
        await EnsureHttpOkAsync(http, new Uri("http://127.0.0.1:19090/-/ready"), "Prometheus readiness", token);
        await EnsureHttpOkAsync(http, new Uri("http://127.0.0.1:13000/api/health"), "Grafana health", token);
    }

    private static async Task EnsureHttpOkAsync(HttpClient http, Uri uri, string name, CancellationToken token)
    {
        using var response = await http.GetAsync(uri, token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{name} devolvió HTTP {(int)response.StatusCode}; no se ejecuta la prueba.");
    }

    private static async Task<string> GitCommitAsync(CommandRunner commands, string root, CancellationToken token)
    {
        var result = await commands.RunAsync("git", ["-C", root, "rev-parse", "--short", "HEAD"], cancellationToken: token);
        return result.ExitCode == 0 ? result.StandardOutput.Trim() : "desconocido";
    }

    private static async Task<Dictionary<string, string>> ToolVersionsAsync(CommandRunner commands, CancellationToken token)
    {
        var versions = new Dictionary<string, string>();
        foreach (var (tool, args) in new[] { ("dotnet", new[] { "--version" }), ("docker", new[] { "--version" }), ("kubectl", new[] { "version", "--client", "-o", "json" }), ("tilt", new[] { "version" }) })
        {
            try { var result = await commands.RunAsync(tool, args, timeout: TimeSpan.FromSeconds(10), cancellationToken: token); versions[tool] = result.ExitCode == 0 ? result.StandardOutput.Trim() : "no disponible"; }
            catch { versions[tool] = "no disponible"; }
        }
        return versions;
    }

    private static void ValidateImmutableTag(string image)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(image, "^[A-Za-z0-9._/-]+:[A-Za-z0-9._-]+$") || image.EndsWith(":latest", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La imagen debe ser local, tener tag inmutable explícito y no usar latest.");
    }

    private static string Sanitize(string message) => System.Text.RegularExpressions.Regex.Replace(message, "(?i)(password|token|secret|connectionstring)=?[^\\s;]+", "$1=[redactado]");
    private static void PrintHelp() => Console.WriteLine("OrbiApp.LoadTestRunner: doctor | smoke | baseline | matrix [--max-vus 20|50|100|200|500 --allow-stress --lab-only --confirm-multi-replica] | availability --confirm-pod-delete --lab-only | rollout --from IMAGE --to IMAGE --lab-only | report | events | lab-secret");
}
