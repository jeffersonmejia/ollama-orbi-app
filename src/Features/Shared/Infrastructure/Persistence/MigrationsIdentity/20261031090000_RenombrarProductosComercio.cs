using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrbiApp.Data;

namespace OrbiApp.Data.MigrationsIdentity;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261031090000_RenombrarProductosComercio")]
public sealed class RenombrarProductosComercio : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "FilmStocks", newName: "Products");
        migrationBuilder.RenameColumn(name: "FilmStockId", table: "Products", newName: "ProductId");
        migrationBuilder.RenameColumn(name: "FilmId", table: "Products", newName: "SourceId");
        migrationBuilder.RenameColumn(name: "Title", table: "Products", newName: "Name");
        migrationBuilder.RenameColumn(name: "FilmStockId", table: "ShoppingCartItems", newName: "ProductId");
        migrationBuilder.RenameColumn(name: "FilmStockId", table: "PurchaseOrderDetails", newName: "ProductId");
        migrationBuilder.RenameColumn(name: "FilmTitle", table: "PurchaseOrderDetails", newName: "ProductName");
        migrationBuilder.RenameIndex(
            name: "IX_ShoppingCartItems_FilmStockId",
            table: "ShoppingCartItems",
            newName: "IX_ShoppingCartItems_ProductId");
        migrationBuilder.Sql("""
            ALTER TABLE "Products" RENAME CONSTRAINT "PK_FilmStocks" TO "PK_Products";
            ALTER TABLE "ShoppingCartItems" RENAME CONSTRAINT "FK_ShoppingCartItems_FilmStocks_FilmStockId" TO "FK_ShoppingCartItems_Products_ProductId";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "ShoppingCartItems" RENAME CONSTRAINT "FK_ShoppingCartItems_Products_ProductId" TO "FK_ShoppingCartItems_FilmStocks_FilmStockId";
            ALTER TABLE "Products" RENAME CONSTRAINT "PK_Products" TO "PK_FilmStocks";
            """);
        migrationBuilder.RenameIndex(
            name: "IX_ShoppingCartItems_ProductId",
            table: "ShoppingCartItems",
            newName: "IX_ShoppingCartItems_FilmStockId");
        migrationBuilder.RenameColumn(name: "ProductName", table: "PurchaseOrderDetails", newName: "FilmTitle");
        migrationBuilder.RenameColumn(name: "ProductId", table: "PurchaseOrderDetails", newName: "FilmStockId");
        migrationBuilder.RenameColumn(name: "ProductId", table: "ShoppingCartItems", newName: "FilmStockId");
        migrationBuilder.RenameColumn(name: "Name", table: "Products", newName: "Title");
        migrationBuilder.RenameColumn(name: "SourceId", table: "Products", newName: "FilmId");
        migrationBuilder.RenameColumn(name: "ProductId", table: "Products", newName: "FilmStockId");
        migrationBuilder.RenameTable(name: "Products", newName: "FilmStocks");
    }
}
