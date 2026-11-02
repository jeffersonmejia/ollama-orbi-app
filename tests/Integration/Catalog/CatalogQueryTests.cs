using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrbiApp.Integration.Tests;

[Collection(PostgresDatabaseCollection.Name)]
public sealed class CatalogQueryTests(PostgresDatabaseFixture fixture)
{
    [Fact]
    public async Task Search_filters_join_count_and_page_return_expected_records()
    {
        await using var db = fixture.CreateContext();
        var query = DatabaseTestQueries.Catalog(db)
            .Where(product => product.Store.CityCode == fixture.CityCode)
            .Where(product => product.Price >= 10m && product.Price <= 40m)
            .Where(product => product.Store.Category == "Farmacia")
            .Where(product => EF.Functions.ILike(product.Name, "%catalog-hit%") ||
                              EF.Functions.ILike(product.Store.Name, "%catalog-hit%") ||
                              EF.Functions.ILike(product.Store.Category, "%catalog-hit%"));

        var total = await query.CountAsync();
        var firstPage = await query.OrderBy(product => product.Store.Name).ThenBy(product => product.Name)
            .Take(12).ToListAsync();

        Assert.Equal(240, total);
        Assert.Equal(12, firstPage.Count);
        Assert.All(firstPage, product =>
        {
            Assert.True(product.IsAvailable);
            Assert.True(product.Store.IsActive);
            Assert.Equal(fixture.CityCode, product.Store.CityCode);
            Assert.InRange(product.Price, 10m, 40m);
            Assert.Contains("catalog-hit", product.Name, StringComparison.OrdinalIgnoreCase);
        });
    }
}
