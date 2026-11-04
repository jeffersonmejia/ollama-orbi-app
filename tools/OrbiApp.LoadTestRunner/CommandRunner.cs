using System.Diagnostics;

namespace OrbiApp.LoadTestRunner;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class CommandRunner
{
    public async Task<CommandResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null,
        string? standardInput = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
                startInfo.Environment[key] = value;
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException($"No se pudo iniciar {Path.GetFileName(executable)}.");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout ?? TimeSpan.FromMinutes(2));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), timeoutSource.Token);
            process.StandardInput.Close();
        }

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return new CommandResult(process.ExitCode, await stdoutTask, await stderrTask);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (cancellationToken.IsCancellationRequested)
                throw;
            return new CommandResult(124, await SafeRead(stdoutTask), await SafeRead(stderrTask));
        }
    }

    private static async Task<string> SafeRead(Task<string> task)
    {
        try { return await task; }
        catch (OperationCanceledException) { return string.Empty; }
    }
}
