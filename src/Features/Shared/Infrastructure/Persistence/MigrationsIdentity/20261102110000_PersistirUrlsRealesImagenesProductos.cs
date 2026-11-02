using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrbiApp.Data;

namespace OrbiApp.Data.MigrationsIdentity;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261102110000_PersistirUrlsRealesImagenesProductos")]
public sealed class PersistirUrlsRealesImagenesProductos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE delivery_product AS product
            SET image_url = images.image_url
            FROM (VALUES
                ('Paracetamol Genfar 500 mg caja x10', 'https://images.rappi.com/products/833369070492_wihncvjpydbe_472684072556_wtvxtidmaskp_5049717_1.png?d=900x750&e=webp&q=30'),
                ('Ibuprofeno Genfar 400 mg caja x10', 'https://farmaciauniversalpe.vtexassets.com/arquivos/ids/159167/00890_1.jpg?v=638590981889170000'),
                ('Alcohol antiséptico Coral 250 ml', 'https://coralhipermercados.com/media/catalog/product/5/5/55LAA02-UN_1789696971006.jpg?optimize=medium&bg-color=255,255,255&fit=bounds&height=265&width=265&canvas=265:265'),
                ('Suero oral Medigener coco 500 ml', 'https://www.fybeca.com/dw/image/v2/BDPM_PRD/on/demandware.static/-/Sites-masterCatalog_FybecaEcuador/default/dwa67d5f5f/images/large/100169534-SUERO-ORAL-MEDIGENER-45-COCO-500-ML-FRASCO.JPG?sh=1000&sw=1000'),
                ('Hidraplus zinc fresa 400 ml', 'https://www.fybeca.com/dw/image/v2/BDPM_PRD/on/demandware.static/-/Sites-masterCatalog_FybecaEcuador/default/dwbba0019a/images/large/100034540-hidraplus-45meq-zinc-fresa-400-ml-botella.jpg?sh=1000&sw=1000'),
                ('Vitamina C gomitas 36 g', 'https://www.fybeca.com/on/demandware.static/-/Sites-masterCatalog_FybecaEcuador/default/dw4b05ec91/images/large/100207337-GOMITAS-MASTICABLES-VITAMINA-C-ECUAGEN-36-GR-PAQUETE.jpg'),
                ('Chupete vitamina C y zinc caja x10', 'https://www.elarbolito.com.ec/web/image/product.template/12181/image_1024?unique=71962eb'),
                ('Aceite de castor Weir 30 ml', 'https://farmaenlace.vtexassets.com/arquivos/ids/164118/0000128099-1.jpg?v=638083714171370000')
            ) AS images(name, image_url)
            WHERE product.name = images.name;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE delivery_product
            SET image_url = CASE lower(name)
                WHEN 'paracetamol genfar 500 mg caja x10' THEN '/assets/products/paracetamol.svg'
                WHEN 'ibuprofeno genfar 400 mg caja x10' THEN '/assets/products/ibuprofeno.svg'
                WHEN 'alcohol antiséptico coral 250 ml' THEN '/assets/products/alcohol-antiseptico.svg'
                WHEN 'suero oral medigener coco 500 ml' THEN '/assets/products/suero-oral.svg'
                WHEN 'hidraplus zinc fresa 400 ml' THEN '/assets/products/hidraplus.svg'
                WHEN 'vitamina c gomitas 36 g' THEN '/assets/products/vitamina-c-gomitas.svg'
                WHEN 'chupete vitamina c y zinc caja x10' THEN '/assets/products/chupete-vitamina-c.svg'
                WHEN 'aceite de castor weir 30 ml' THEN '/assets/products/aceite-castor.svg'
                ELSE '/assets/products/producto-default.svg'
            END;
            """);
    }
}
