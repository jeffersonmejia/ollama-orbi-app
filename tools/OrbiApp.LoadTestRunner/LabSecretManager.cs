using System.Text;
using System.Text.Json;
using Npgsql;

namespace OrbiApp.LoadTestRunner;

public sealed class LabSecretManager(string repositoryRoot, KubernetesController kubernetes)
{
    public async Task EnsureSecretAsync(CancellationToken cancellationToken = default)
    {
        await kubernetes.EnsureLocalContextAsync(cancellationToken);
        await EnsureOwnedNamespaceAsync(cancellationToken);

        var secretPath = Path.GetFullPath(Path.Combine(repositoryRoot, ".docker", "secrets", "orbi_app_password"));
        var expectedRoot = Path.GetFullPath(Path.Combine(repositoryRoot, ".docker", "secrets")) + Path.DirectorySeparatorChar;
        if (!secretPath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(secretPath))
            throw new InvalidOperationException("Falta .docker/secrets/orbi_app_password. El runner no creará ni imprimirá una contraseña.");

        var existing = await kubernetes.RunKubectlAsync(
            ["get", "secret", "orbi-app-secrets", "-n", KubernetesController.Namespace, "-o", "jsonpath={.metadata.labels.app\\.kubernetes\\.io/part-of}"],
            cancellationToken);
        if (existing.ExitCode != 0 && !existing.StandardError.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No se pudo inspeccionar el Secret existente; no se sobrescribe.");
        if (existing.ExitCode == 0 && !string.Equals(existing.StandardOutput.Trim(), "orbi-load-test-lab", StringComparison.Ordinal))
            throw new InvalidOperationException("Ya existe un Secret orbi-app-secrets que no pertenece al laboratorio; no se sobrescribe.");
        if (existing.ExitCode != 0 && !existing.StandardError.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No se pudo inspeccionar el Secret del laboratorio; no se sobrescribe.");

        var password = (await File.ReadAllTextAsync(secretPath, cancellationToken)).Trim();
        if (password.Length == 0)
            throw new InvalidOperationException("El archivo de contraseña de PostgreSQL está vacío.");
        var host = Environment.GetEnvironmentVariable("ORBI_LAB_DB_HOST") ?? "host.docker.internal";
        if (!string.Equals(host, "host.docker.internal", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ORBI_LAB_DB_HOST solo admite host.docker.internal para este laboratorio local.");
        var port = ReadPort("ORBI_LAB_DB_PORT", 5433);
        var database = Environment.GetEnvironmentVariable("ORBI_LAB_DB_NAME") ?? "orbi_app";
        var username = Environment.GetEnvironmentVariable("ORBI_LAB_DB_USER") ?? "orbi_app";
        if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^[a-zA-Z0-9_-]{1,63}$") ||
            !System.Text.RegularExpressions.Regex.IsMatch(username, "^[a-zA-Z0-9_-]{1,63}$"))
            throw new InvalidOperationException("Nombre de base o usuario no válido para el laboratorio.");

        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            Timeout = 5,
            CommandTimeout = 5,
            Pooling = true,
            MaxPoolSize = 64,
            ApplicationName = "OrbiLoadTestLab",
        }.ConnectionString;
        var secret = new
        {
            apiVersion = "v1",
            kind = "Secret",
            metadata = new
            {
                name = "orbi-app-secrets",
                @namespace = KubernetesController.Namespace,
                labels = new Dictionary<string, string>
                {
                    ["app.kubernetes.io/part-of"] = "orbi-load-test-lab",
                    ["environment"] = "lab",
                },
            },
            type = "Opaque",
            data = new Dictionary<string, string>
            {
                ["DefaultConnection"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(connection)),
            },
        };
        var manifest = JsonSerializer.Serialize(secret);
        var result = await kubernetes.ApplySecretAsync(manifest, cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("No se pudo aplicar el Secret de Kubernetes. La salida se omitió por seguridad.");
        Console.WriteLine("Secret orbi-app-secrets sincronizado con el namespace orbi-lab (valor omitido).");
    }

    private async Task EnsureOwnedNamespaceAsync(CancellationToken cancellationToken)
    {
        await kubernetes.EnsureNamespaceSafeForApplyAsync(cancellationToken);
    }

    private static int ReadPort(string name, int fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException($"{name} debe estar entre 1 y 65535.");
        return port;
    }
}
