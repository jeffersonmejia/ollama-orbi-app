using System.Reflection;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Data;
using Xunit;

namespace OrbiApp.Integration.Tests;

[Collection(PostgresDatabaseCollection.Name)]
public sealed class MappedTableQueryTests(PostgresDatabaseFixture fixture)
{
    [Fact]
    public async Task Every_mapped_entity_table_accepts_a_read_query()
    {
        await using var db = fixture.CreateContext();
        var entityTypes = db.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null &&
                             entity.FindPrimaryKey() is not null &&
                             !entity.IsOwned() &&
                             !entity.HasSharedClrType)
            .Select(entity => entity.ClrType)
            .Distinct()
            .ToArray();
        var readMethod = typeof(MappedTableQueryTests).GetMethod(
            nameof(ReadFirstAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

        foreach (var entityType in entityTypes)
        {
            var task = (Task)readMethod.MakeGenericMethod(entityType).Invoke(null, [db])!;
            await task;
        }

        Assert.NotEmpty(entityTypes);
    }

    private static async Task ReadFirstAsync<TEntity>(ApplicationDbContext db) where TEntity : class
    {
        _ = await db.Set<TEntity>().AsNoTracking().Take(1).ToListAsync();
    }
}
