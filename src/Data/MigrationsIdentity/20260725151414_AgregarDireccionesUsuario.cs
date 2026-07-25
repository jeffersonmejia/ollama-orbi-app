using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SakilaApp.Data.MigrationsIdentity
{
    /// <inheritdoc />
    public partial class AgregarDireccionesUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_address",
                columns: table => new
                {
                    user_address_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    identity_user_id = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    address_line_1 = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    address_line_2 = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    province_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    city_code = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    reference = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_address", x => x.user_address_id);
                    table.CheckConstraint("ck_user_address_label", "length(trim(label)) > 0");
                    table.ForeignKey(
                        name: "FK_user_address_ecuador_city_city_code",
                        column: x => x.city_code,
                        principalTable: "ecuador_city",
                        principalColumn: "city_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_address_ecuador_province_province_code",
                        column: x => x.province_code,
                        principalTable: "ecuador_province",
                        principalColumn: "province_code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_address_user_profile_identity_user_id",
                        column: x => x.identity_user_id,
                        principalTable: "user_profile",
                        principalColumn: "identity_user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_address_city_code",
                table: "user_address",
                column: "city_code");

            migrationBuilder.CreateIndex(
                name: "IX_user_address_province_code",
                table: "user_address",
                column: "province_code");

            migrationBuilder.CreateIndex(
                name: "ux_user_address_default",
                table: "user_address",
                column: "identity_user_id",
                unique: true,
                filter: "is_default");

            migrationBuilder.CreateIndex(
                name: "ux_user_address_user_label",
                table: "user_address",
                columns: new[] { "identity_user_id", "label" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO user_address
                    (identity_user_id, label, address_line_1, address_line_2, province_code, city_code, reference, is_default, created_at, updated_at)
                SELECT identity_user_id, 'Casa', address_line_1, address_line_2, province_code, city_code, reference, TRUE, created_at, now()
                FROM user_profile
                WHERE length(trim(address_line_1)) > 0
                ON CONFLICT (identity_user_id, label) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_address");
        }
    }
}
