using Microsoft.EntityFrameworkCore.Migrations;

namespace OrbiApp.Data.MigrationsIdentity;

public sealed partial class OptimizeDeliveryQueryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // IF NOT EXISTS is required because the legacy schema initializer may have
        // created some of these indexes before EF recorded this migration.
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_store_city_active_name ON delivery_store(city_code, is_active, name);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_store_province_name ON delivery_store(province_code, name);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_product_store_available_name ON delivery_product(delivery_store_id, is_available, name);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_product_store_created_at ON delivery_product(delivery_store_id, created_at);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_order_customer_created_at ON delivery_order(customer_email, created_at);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_order_driver_created_at ON delivery_order(delivery_person_email, created_at);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_order_store_created_at ON delivery_order(delivery_store_id, created_at);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_order_item_order ON delivery_order_item(delivery_order_id);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_delivery_order_item_product ON delivery_order_item(delivery_product_id);");
        migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_user_profile_city_code ON user_profile(city_code);");
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF to_regclass('public.delivery_cart_item') IS NOT NULL THEN
                    CREATE INDEX IF NOT EXISTS ix_delivery_cart_product ON delivery_cart_item(delivery_product_id);
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_cart_product;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_user_profile_city_code;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_order_item_product;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_order_item_order;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_order_store_created_at;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_order_driver_created_at;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_order_customer_created_at;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_product_store_created_at;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_product_store_available_name;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_store_province_name;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ix_delivery_store_city_active_name;");
    }
}
