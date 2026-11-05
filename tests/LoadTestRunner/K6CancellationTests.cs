using OrbiApp.LoadTestRunner;
using Xunit;

namespace OrbiApp.LoadTestRunner.Tests;

public sealed class K6CancellationTests
{
    [Fact]
    public async Task StartAsyncStopsBeforeCreatingRunFilesWhenCancellationIsAlreadyRequested()
    {
        var repositoryRoot = Path.Combine(Path.GetTempPath(), $"orbi-cancel-test-{Guid.NewGuid():N}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var executor = new K6Executor(repositoryRoot, new CommandRunner(), new EventPublisher());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.StartAsync(new K6RunRequest("smoke", 1, 10, 1, false), "cancel-before-start", cancellation.Token));

        Assert.False(Directory.Exists(Path.Combine(repositoryRoot, "tests", "k6", "results", "cancel-before-start")));
    }
}
