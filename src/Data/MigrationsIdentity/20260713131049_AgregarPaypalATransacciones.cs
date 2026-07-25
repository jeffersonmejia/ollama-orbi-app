using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SakilaApp.Data.MigrationsIdentity
{
    /// <inheritdoc />
    public partial class AgregarPaypalATransacciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayResponse",
                table: "PaymentTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayPalApprovalUrl",
                table: "PaymentTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayPalCaptureId",
                table: "PaymentTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayPalOrderId",
                table: "PaymentTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "PaymentTransactions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GatewayResponse",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PayPalApprovalUrl",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PayPalCaptureId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PayPalOrderId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "PaymentTransactions");
        }
    }
}
