using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SakilaApp.Data.MigrationsIdentity
{
    /// <inheritdoc />
    public partial class AgregarTablasOperativasOrbi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_consumption_log",
                columns: table => new
                {
                    ai_consumption_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: true),
                    model_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    estimated_cost = table.Column<decimal>(type: "numeric(14,6)", nullable: false),
                    duration_milliseconds = table.Column<int>(type: "integer", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_consumption_log", x => x.ai_consumption_log_id);
                    table.CheckConstraint("ck_ai_consumption_log_cost", "estimated_cost >= 0");
                    table.CheckConstraint("ck_ai_consumption_log_duration", "duration_milliseconds >= 0");
                    table.CheckConstraint("ck_ai_consumption_log_tokens", "prompt_tokens >= 0 AND completion_tokens >= 0 AND total_tokens >= 0");
                    table.ForeignKey(
                        name: "FK_ai_consumption_log_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    audit_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: true),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.audit_log_id);
                    table.ForeignKey(
                        name: "FK_audit_log_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "delivery_incident",
                columns: table => new
                {
                    delivery_incident_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    delivery_order_id = table.Column<int>(type: "integer", nullable: false),
                    reported_by_user_id = table.Column<string>(type: "text", nullable: true),
                    incident_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Media"),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Abierto"),
                    details = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_incident", x => x.delivery_incident_id);
                    table.CheckConstraint("ck_delivery_incident_resolution", "resolved_at IS NULL OR resolved_at >= created_at");
                    table.CheckConstraint("ck_delivery_incident_severity", "severity IN ('Baja', 'Media', 'Alta', 'Crítica')");
                    table.CheckConstraint("ck_delivery_incident_status", "status IN ('Abierto', 'En revisión', 'Resuelto', 'Cerrado')");
                    table.ForeignKey(
                        name: "FK_delivery_incident_AspNetUsers_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_delivery_incident_delivery_order_delivery_order_id",
                        column: x => x.delivery_order_id,
                        principalTable: "delivery_order",
                        principalColumn: "delivery_order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_queue",
                columns: table => new
                {
                    email_queue_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_queue", x => x.email_queue_id);
                    table.CheckConstraint("ck_email_queue_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_email_queue_max_attempts", "max_attempts > 0 AND attempt_count <= max_attempts");
                    table.CheckConstraint("ck_email_queue_status", "status IN ('Pendiente', 'Procesando', 'Enviado', 'Fallido', 'Cancelado')");
                });

            migrationBuilder.CreateTable(
                name: "inventory_movement",
                columns: table => new
                {
                    inventory_movement_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    delivery_product_id = table.Column<int>(type: "integer", nullable: false),
                    delivery_order_id = table.Column<int>(type: "integer", nullable: true),
                    performed_by_user_id = table.Column<string>(type: "text", nullable: true),
                    movement_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity_delta = table.Column<int>(type: "integer", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_movement", x => x.inventory_movement_id);
                    table.CheckConstraint("ck_inventory_movement_quantity_delta", "quantity_delta <> 0");
                    table.CheckConstraint("ck_inventory_movement_type", "movement_type IN ('Entrada', 'Salida', 'Ajuste', 'Reserva', 'Liberación')");
                    table.CheckConstraint("ck_inventory_movement_unit_cost", "unit_cost IS NULL OR unit_cost >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_movement_AspNetUsers_performed_by_user_id",
                        column: x => x.performed_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventory_movement_delivery_order_delivery_order_id",
                        column: x => x.delivery_order_id,
                        principalTable: "delivery_order",
                        principalColumn: "delivery_order_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_inventory_movement_delivery_product_delivery_product_id",
                        column: x => x.delivery_product_id,
                        principalTable: "delivery_product",
                        principalColumn: "delivery_product_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_status_history",
                columns: table => new
                {
                    order_status_history_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    delivery_order_id = table.Column<int>(type: "integer", nullable: false),
                    changed_by_user_id = table.Column<string>(type: "text", nullable: true),
                    previous_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    new_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_history", x => x.order_status_history_id);
                    table.CheckConstraint("ck_order_status_history_new", "new_status IN ('Pendiente', 'En preparación', 'En camino', 'Entregado', 'Cancelado')");
                    table.CheckConstraint("ck_order_status_history_previous", "previous_status IS NULL OR previous_status IN ('Pendiente', 'En preparación', 'En camino', 'Entregado', 'Cancelado')");
                    table.ForeignKey(
                        name: "FK_order_status_history_AspNetUsers_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_order_status_history_delivery_order_delivery_order_id",
                        column: x => x.delivery_order_id,
                        principalTable: "delivery_order",
                        principalColumn: "delivery_order_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservation",
                columns: table => new
                {
                    stock_reservation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    delivery_product_id = table.Column<int>(type: "integer", nullable: false),
                    delivery_order_id = table.Column<int>(type: "integer", nullable: false),
                    reserved_by_user_id = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Activa"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservation", x => x.stock_reservation_id);
                    table.CheckConstraint("ck_stock_reservation_expiration", "expires_at > created_at");
                    table.CheckConstraint("ck_stock_reservation_quantity", "quantity > 0");
                    table.CheckConstraint("ck_stock_reservation_release", "released_at IS NULL OR released_at >= created_at");
                    table.CheckConstraint("ck_stock_reservation_status", "status IN ('Activa', 'Confirmada', 'Liberada', 'Expirada', 'Cancelada')");
                    table.ForeignKey(
                        name: "FK_stock_reservation_AspNetUsers_reserved_by_user_id",
                        column: x => x.reserved_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_stock_reservation_delivery_order_delivery_order_id",
                        column: x => x.delivery_order_id,
                        principalTable: "delivery_order",
                        principalColumn: "delivery_order_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stock_reservation_delivery_product_delivery_product_id",
                        column: x => x.delivery_product_id,
                        principalTable: "delivery_product",
                        principalColumn: "delivery_product_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_consumption_log_created_at",
                table: "ai_consumption_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_consumption_log_model_created_at",
                table: "ai_consumption_log",
                columns: new[] { "model_name", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_consumption_log_user_created_at",
                table: "ai_consumption_log",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_correlation_id",
                table: "audit_log",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_created_at",
                table: "audit_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_user_created_at",
                table: "audit_log",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_incident_created_at",
                table: "delivery_incident",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_incident_order_status",
                table: "delivery_incident",
                columns: new[] { "delivery_order_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_incident_reporter",
                table: "delivery_incident",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_queue_created_at",
                table: "email_queue",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_email_queue_status_scheduled_at",
                table: "email_queue",
                columns: new[] { "status", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movement_order",
                table: "inventory_movement",
                column: "delivery_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movement_product_created_at",
                table: "inventory_movement",
                columns: new[] { "delivery_product_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_movement_user",
                table: "inventory_movement",
                column: "performed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_status_history_order_changed_at",
                table: "order_status_history",
                columns: new[] { "delivery_order_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_order_status_history_user",
                table: "order_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservation_product",
                table: "stock_reservation",
                column: "delivery_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservation_status_expires_at",
                table: "stock_reservation",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservation_user",
                table: "stock_reservation",
                column: "reserved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_stock_reservation_active_order_product",
                table: "stock_reservation",
                columns: new[] { "delivery_order_id", "delivery_product_id" },
                unique: true,
                filter: "status = 'Activa'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_consumption_log");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "delivery_incident");

            migrationBuilder.DropTable(
                name: "email_queue");

            migrationBuilder.DropTable(
                name: "inventory_movement");

            migrationBuilder.DropTable(
                name: "order_status_history");

            migrationBuilder.DropTable(
                name: "stock_reservation");
        }
    }
}
