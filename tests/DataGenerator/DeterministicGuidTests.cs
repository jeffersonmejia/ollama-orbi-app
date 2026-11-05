using OrbiApp.DataGenerator;
using Xunit;

namespace OrbiApp.DataGenerator.Tests;

public sealed class DeterministicGuidTests
{
    [Fact]
    public void SameSeedAndIndexProduceSameNonEmptyUuid()
    {
        var first = OrbiDataGenerator.DeterministicGuid(2026, 42);
        var second = OrbiDataGenerator.DeterministicGuid(2026, 42);

        Assert.Equal(first, second);
        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(36, first.ToString("D").Length);
        Assert.Equal('-', first.ToString("D")[8]);
        Assert.Equal('-', first.ToString("D")[13]);
        Assert.Equal('-', first.ToString("D")[18]);
        Assert.Equal('-', first.ToString("D")[23]);
    }

    [Fact]
    public void DifferentInputsProduceDistinctVersionEightUuidsInSample()
    {
        var values = Enumerable.Range(0, 1_000)
            .Select(index => OrbiDataGenerator.DeterministicGuid(2026, index))
            .ToArray();

        Assert.Equal(values.Length, values.Distinct().Count());
        Assert.All(values, value =>
        {
            Assert.Equal('8', value.ToString("D")[14]);
            Assert.Contains(value.ToString("D")[19], "89ab");
        });
    }

    [Fact]
    public void CanonicalGuidRepresentationIsStable()
    {
        var value = OrbiDataGenerator.DeterministicGuid(2026, 0);

        Assert.Equal(Guid.Parse("adc6ae58-7f26-83c5-a235-d1d55a546335"), value);
        Assert.Equal(36, value.ToString("D").Length);
        Assert.Equal('8', value.ToString("D")[14]);
        Assert.Contains(value.ToString("D")[19], "89ab");
        Assert.Equal(value, Guid.Parse(value.ToString("D")));
    }

    [Fact]
    public void UserIdentifierSeedsAndRelatedUserReferencesRemainDistinctAndReproducible()
    {
        var userId = OrbiDataGenerator.DeterministicGuid(2026, 17);
        var securityStamp = OrbiDataGenerator.DeterministicGuid(2027, 17);
        var concurrencyStamp = OrbiDataGenerator.DeterministicGuid(2028, 17);

        Assert.NotEqual(userId, securityStamp);
        Assert.NotEqual(userId, concurrencyStamp);
        Assert.Equal(userId, OrbiDataGenerator.DeterministicGuid(2026, 17));
    }
}
