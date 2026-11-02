using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrbiApp.Database.Tests;

[Collection(PostgresDatabaseCollection.Name)]
public sealed class DatabaseIndexTests(PostgresDatabaseFixture fixture)
{
    [Fact]
    public async Task Hot_query_and_foreign_key_columns_have_supporting_indexes()
    {
        await using var db = fixture.CreateContext();
        var indexes = await db.Database.SqlQueryRaw<string>("""
            SELECT indexname AS "Value" FROM pg_indexes WHERE schemaname = 'public'
              AND tablename IN ('delivery_store', 'delivery_product', 'delivery_order',
                'delivery_order_item', 'user_profile', 'delivery_cart_item')
            """).ToListAsync();

        Assert.Contains("ix_delivery_store_city_active_name", indexes);
        Assert.Contains("ix_delivery_store_province_name", indexes);
        Assert.Contains("ix_delivery_product_store_available_name", indexes);
        Assert.Contains("ix_delivery_product_store_created_at", indexes);
        Assert.Contains("ix_delivery_order_customer_created_at", indexes);
        Assert.Contains("ix_delivery_order_driver_created_at", indexes);
        Assert.Contains("ix_delivery_order_store_created_at", indexes);
        Assert.Contains("ix_delivery_order_item_order", indexes);
        Assert.Contains("ix_delivery_order_item_product", indexes);
        Assert.Contains("ix_user_profile_city_code", indexes);
        Assert.Contains("ix_delivery_cart_product", indexes);

        var missingForeignKeyIndexes = await db.Database.SqlQueryRaw<string>("""
            SELECT conrelid::regclass::text || '.' || conname AS "Value"
            FROM pg_constraint AS constraint_row
            WHERE constraint_row.contype = 'f' AND constraint_row.connamespace = 'public'::regnamespace
              AND NOT EXISTS (
                SELECT 1 FROM pg_index AS index_row
                WHERE index_row.indrelid = constraint_row.conrelid AND index_row.indisvalid AND index_row.indisready
                  AND (SELECT array_agg(index_key.attnum::smallint ORDER BY index_key.ordinality)
                       FROM unnest(index_row.indkey::smallint[]) WITH ORDINALITY AS index_key(attnum, ordinality)
                       WHERE index_key.ordinality <= cardinality(constraint_row.conkey)) = constraint_row.conkey)
            ORDER BY 1
            """).ToListAsync();
        Assert.Empty(missingForeignKeyIndexes);
    }
}
