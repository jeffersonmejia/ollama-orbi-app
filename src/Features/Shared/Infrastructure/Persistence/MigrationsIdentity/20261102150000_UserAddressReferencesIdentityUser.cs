using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrbiApp.Data;

namespace OrbiApp.Data.MigrationsIdentity;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261102150000_UserAddressReferencesIdentityUser")]
public sealed class UserAddressReferencesIdentityUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_user_address_user_profile_identity_user_id",
            table: "user_address");

        migrationBuilder.AddForeignKey(
            name: "FK_user_address_AspNetUsers_identity_user_id",
            table: "user_address",
            column: "identity_user_id",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM user_address AS address
                    LEFT JOIN user_profile AS profile
                        ON profile.identity_user_id = address.identity_user_id
                    WHERE profile.identity_user_id IS NULL
                ) THEN
                    RAISE EXCEPTION 'Cannot restore the user_profile foreign key while addresses exist for users without profiles.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_user_address_AspNetUsers_identity_user_id",
            table: "user_address");

        migrationBuilder.AddForeignKey(
            name: "FK_user_address_user_profile_identity_user_id",
            table: "user_address",
            column: "identity_user_id",
            principalTable: "user_profile",
            principalColumn: "identity_user_id",
            onDelete: ReferentialAction.Cascade);
    }
}
