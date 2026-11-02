using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrbiApp.Data;

namespace OrbiApp.Data.MigrationsIdentity;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261102090000_AgregarImagenesProductosDelivery")]
public sealed class AgregarImagenesProductosDelivery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "image_url",
            table: "delivery_product",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE delivery_product
            SET image_url = CASE
                WHEN lower(name) LIKE '%paracetamol%' THEN '/assets/products/paracetamol.svg'
                WHEN lower(name) LIKE '%ibuprofeno%' THEN '/assets/products/ibuprofeno.svg'
                WHEN lower(name) LIKE '%alcohol antiséptico%' OR lower(name) LIKE '%alcohol antiseptico%' THEN '/assets/products/alcohol-antiseptico.svg'
                WHEN lower(name) LIKE '%suero oral%' THEN '/assets/products/suero-oral.svg'
                WHEN lower(name) LIKE '%hidraplus%' THEN '/assets/products/hidraplus.svg'
                WHEN lower(name) LIKE '%vitamina c gomitas%' THEN '/assets/products/vitamina-c-gomitas.svg'
                WHEN lower(name) LIKE '%chupete vitamina c%' THEN '/assets/products/chupete-vitamina-c.svg'
                WHEN lower(name) LIKE '%aceite de castor%' THEN '/assets/products/aceite-castor.svg'
                ELSE '/assets/products/producto-default.svg'
            END
            WHERE image_url IS NULL OR btrim(image_url) = '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "image_url", table: "delivery_product");
    }
}
