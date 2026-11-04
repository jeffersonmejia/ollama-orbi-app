using OrbiApp.LoadTestRunner;
using Xunit;

namespace OrbiApp.LoadTestRunner.Tests;

public sealed class RunnerOptionsTests
{
    [Fact]
    public void MatrixPlannerCreatesFullOrderedFifteenCases()
    {
        var cases = MatrixPlanner.Plan(500);
        Assert.Equal(15, cases.Count);
        Assert.Equal(new[] { 1, 3, 10 }, cases.Select(item => item.Replicas).Distinct());
        Assert.Equal(new[] { 20, 50, 100, 200, 500 }, cases.Take(5).Select(item => item.Vus));
        Assert.Equal(15, cases.Select(item => (item.Replicas, item.Vus)).Distinct().Count());
    }

    [Fact]
    public void MatrixPlannerCapsAtRequestedLevel()
    {
        Assert.Equal(9, MatrixPlanner.Plan(100).Count);
    }

    [Theory]
    [InlineData("200")]
    [InlineData("500")]
    public void StressRequiresBothExplicitFlags(string maxVus)
    {
        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(["matrix", "--max-vus", maxVus]));
        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(["matrix", "--max-vus", maxVus, "--allow-stress"]));
        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(["matrix", "--max-vus", maxVus, "--lab-only"]));
        var options = RunnerOptions.Parse(["matrix", "--max-vus", maxVus, "--allow-stress", "--lab-only"]);
        Assert.True(options.AllowStress);
        Assert.True(options.LabOnly);
    }

    [Fact]
    public void AvailabilityRequiresExplicitLabAndDeletionFlags()
    {
        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(["availability"]));
        Assert.Throws<ArgumentException>(() => RunnerOptions.Parse(["availability", "--confirm-pod-delete"]));
        Assert.True(RunnerOptions.Parse(["availability", "--confirm-pod-delete", "--lab-only"]).ConfirmPodDelete);
    }
}
