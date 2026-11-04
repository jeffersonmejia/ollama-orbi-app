using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrbiApp.Data;

namespace OrbiApp.Data.MigrationsIdentity;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261102193000_EnforceSingleStoreAndAddress")]
public sealed class EnforceSingleStoreAndAddress : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE delivery_store
            SET category = CASE lower(trim(category))
                WHEN 'restaurante' THEN 'Restaurantes'
                WHEN 'restaurantes' THEN 'Restaurantes'
                WHEN 'cafetería' THEN 'Restaurantes'
                WHEN 'cafeteria' THEN 'Restaurantes'
                WHEN 'panadería' THEN 'Restaurantes'
                WHEN 'panaderia' THEN 'Restaurantes'
                WHEN 'farmacia' THEN 'Farmacia'
                WHEN 'farmacias' THEN 'Farmacia'
                WHEN 'supermercado' THEN 'Supermercado'
                WHEN 'supermercados' THEN 'Supermercado'
                WHEN 'alimentos' THEN 'Supermercado'
                WHEN 'gourmet' THEN 'Supermercado'
                ELSE category
            END;
            """);

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM delivery_store
                    WHERE category NOT IN ('Restaurantes', 'Farmacia', 'Supermercado')
                ) THEN
                    RAISE EXCEPTION 'Cannot enforce supported delivery store categories: review delivery_store rows outside the three supported categories.';
                END IF;
            END $$;
            """);

        // Preserve legacy duplicates in reversible archive tables, then keep one
        // active address and one active seller-owned store per account.
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS user_address_legacy_archive AS
            SELECT address.*, now() AS archived_at FROM user_address AS address WHERE false;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_user_address_legacy_archive_id
                ON user_address_legacy_archive(user_address_id);

            INSERT INTO user_address_legacy_archive
            SELECT address.*, now()
            FROM user_address AS address
            JOIN (
                SELECT user_address_id,
                       row_number() OVER (
                           PARTITION BY identity_user_id
                           ORDER BY is_default DESC, updated_at DESC, user_address_id
                       ) AS row_number
                FROM user_address
            ) AS ranked USING (user_address_id)
            WHERE ranked.row_number > 1
            ON CONFLICT (user_address_id) DO NOTHING;

            DELETE FROM user_address AS address
            USING (
                SELECT user_address_id,
                       row_number() OVER (
                           PARTITION BY identity_user_id
                           ORDER BY is_default DESC, updated_at DESC, user_address_id
                       ) AS row_number
                FROM user_address
            ) AS ranked
            WHERE address.user_address_id = ranked.user_address_id
              AND ranked.row_number > 1;

            UPDATE user_address SET is_default = true;

            CREATE TABLE IF NOT EXISTS delivery_store_owner_legacy_archive AS
            SELECT delivery_store_id,
                   owner_user_id AS former_owner_user_id,
                   is_active AS former_is_active,
                   now() AS archived_at
            FROM delivery_store WHERE false;
            CREATE UNIQUE INDEX IF NOT EXISTS ux_delivery_store_owner_legacy_archive_id
                ON delivery_store_owner_legacy_archive(delivery_store_id);

            INSERT INTO delivery_store_owner_legacy_archive
            SELECT delivery_store_id, owner_user_id, is_active, now()
            FROM (
                SELECT delivery_store_id, owner_user_id, is_active,
                       row_number() OVER (
                           PARTITION BY owner_user_id
                           ORDER BY created_at, delivery_store_id
                       ) AS row_number
                FROM delivery_store
                WHERE owner_user_id IS NOT NULL
            ) AS ranked
            WHERE ranked.row_number > 1
            ON CONFLICT (delivery_store_id) DO NOTHING;

            UPDATE delivery_store AS store
            SET owner_user_id = NULL, is_active = false
            FROM delivery_store_owner_legacy_archive AS archived
            WHERE store.delivery_store_id = archived.delivery_store_id;
            """);

        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ux_user_address_one_per_user ON user_address(identity_user_id);");
        migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ux_delivery_store_owner ON delivery_store(owner_user_id) WHERE owner_user_id IS NOT NULL;");
        migrationBuilder.Sql("ALTER TABLE delivery_store ALTER COLUMN address TYPE varchar(360);");
        migrationBuilder.Sql("""
            UPDATE delivery_store AS store
            SET address = trim(concat_ws(' ', nullif(trim(address.address_line_1), ''), nullif(trim(address.address_line_2), ''))),
                province_code = address.province_code,
                city_code = address.city_code
            FROM user_address AS address
            WHERE store.owner_user_id = address.identity_user_id;
            """);
        migrationBuilder.Sql("ALTER TABLE delivery_store ADD CONSTRAINT ck_delivery_store_category CHECK (category IN ('Restaurantes', 'Farmacia', 'Supermercado'));");
        migrationBuilder.Sql("UPDATE delivery_product SET is_available = false WHERE stock <= 0 AND is_available;");
        migrationBuilder.Sql("ALTER TABLE delivery_product ADD CONSTRAINT ck_delivery_product_availability CHECK (NOT is_available OR stock > 0);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE delivery_store DROP CONSTRAINT IF EXISTS ck_delivery_store_category;");
        migrationBuilder.Sql("ALTER TABLE delivery_product DROP CONSTRAINT IF EXISTS ck_delivery_product_availability;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ux_delivery_store_owner;");
        migrationBuilder.Sql("DROP INDEX IF EXISTS ux_user_address_one_per_user;");
        migrationBuilder.Sql("""
            INSERT INTO user_address (
                user_address_id, identity_user_id, label, address_line_1, address_line_2,
                province_code, city_code, reference, is_default, created_at, updated_at)
            SELECT user_address_id, identity_user_id, label, address_line_1, address_line_2,
                   province_code, city_code, reference, is_default, created_at, updated_at
            FROM user_address_legacy_archive
            ON CONFLICT (user_address_id) DO NOTHING;

            UPDATE delivery_store AS store
            SET owner_user_id = archived.former_owner_user_id,
                is_active = archived.former_is_active
            FROM delivery_store_owner_legacy_archive AS archived
            WHERE store.delivery_store_id = archived.delivery_store_id;

            DROP TABLE IF EXISTS user_address_legacy_archive;
            DROP TABLE IF EXISTS delivery_store_owner_legacy_archive;
            """);
    }
}
