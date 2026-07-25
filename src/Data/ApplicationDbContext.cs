using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SakilaApp.Models.Commerce;
using SakilaApp.Models.Delivery;
using SakilaApp.Models.Identity;
using SakilaApp.Models.Operations;

namespace SakilaApp.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<FilmStock> FilmStocks => Set<FilmStock>();
    public DbSet<ShoppingCartItem> ShoppingCartItems => Set<ShoppingCartItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderDetail> PurchaseOrderDetails => Set<PurchaseOrderDetail>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<DeliveryStore> DeliveryStores => Set<DeliveryStore>();
    public DbSet<DeliveryProduct> DeliveryProducts => Set<DeliveryProduct>();
    public DbSet<DeliveryOrder> DeliveryOrders => Set<DeliveryOrder>();
    public DbSet<DeliveryOrderItem> DeliveryOrderItems => Set<DeliveryOrderItem>();
    public DbSet<EcuadorProvince> EcuadorProvinces => Set<EcuadorProvince>();
    public DbSet<EcuadorCity> EcuadorCities => Set<EcuadorCity>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<DeliveryIncident> DeliveryIncidents => Set<DeliveryIncident>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<EmailQueueItem> EmailQueue => Set<EmailQueueItem>();
    public DbSet<AiConsumptionLog> AiConsumptionLogs => Set<AiConsumptionLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<EcuadorProvince>(entity =>
        {
            entity.ToTable("ecuador_province");
            entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasColumnName("province_code").HasMaxLength(2);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        });

        builder.Entity<EcuadorCity>(entity =>
        {
            entity.ToTable("ecuador_city");
            entity.HasKey(x => x.Code);
            entity.Property(x => x.Code).HasColumnName("city_code").HasMaxLength(4);
            entity.Property(x => x.ProvinceCode).HasColumnName("province_code").HasMaxLength(2);
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            entity.HasOne(x => x.Province)
                .WithMany(x => x.Cities)
                .HasForeignKey(x => x.ProvinceCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profile");
            entity.HasKey(x => x.IdentityUserId);
            entity.Property(x => x.IdentityUserId).HasColumnName("identity_user_id");
            entity.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(80);
            entity.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(80);
            entity.Property(x => x.Cedula).HasColumnName("cedula").HasMaxLength(10);
            entity.Property(x => x.AddressLine1).HasColumnName("address_line_1").HasMaxLength(160);
            entity.Property(x => x.AddressLine2).HasColumnName("address_line_2").HasMaxLength(160);
            entity.Property(x => x.ProvinceCode).HasColumnName("province_code").HasMaxLength(2);
            entity.Property(x => x.CityCode).HasColumnName("city_code").HasMaxLength(4);
            entity.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(240);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.Cedula).IsUnique();
            entity.HasOne<Microsoft.AspNetCore.Identity.IdentityUser>()
                .WithOne()
                .HasForeignKey<UserProfile>(x => x.IdentityUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Province)
                .WithMany()
                .HasForeignKey(x => x.ProvinceCode)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.City)
                .WithMany()
                .HasForeignKey(x => x.CityCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DeliveryStore>(entity =>
        {
            entity.ToTable("delivery_store");
            entity.Property(x => x.DeliveryStoreId).HasColumnName("delivery_store_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Category).HasColumnName("category");
            entity.Property(x => x.Address).HasColumnName("address");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
        });

        builder.Entity<DeliveryProduct>(entity =>
        {
            entity.ToTable("delivery_product");
            entity.Property(x => x.DeliveryProductId).HasColumnName("delivery_product_id");
            entity.Property(x => x.DeliveryStoreId).HasColumnName("delivery_store_id");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.Price).HasColumnName("price");
            entity.Property(x => x.IsAvailable).HasColumnName("is_available");
        });

        builder.Entity<DeliveryOrder>(entity =>
        {
            entity.ToTable("delivery_order");
            entity.Property(x => x.DeliveryOrderId).HasColumnName("delivery_order_id");
            entity.Property(x => x.DeliveryStoreId).HasColumnName("delivery_store_id");
            entity.Property(x => x.CustomerEmail).HasColumnName("customer_email");
            entity.Property(x => x.DeliveryPersonEmail).HasColumnName("delivery_person_email");
            entity.Property(x => x.DeliveryAddress).HasColumnName("delivery_address");
            entity.Property(x => x.Status).HasColumnName("status");
            entity.Property(x => x.Total).HasColumnName("total");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        builder.Entity<DeliveryOrderItem>(entity =>
        {
            entity.ToTable("delivery_order_item");
            entity.Property(x => x.DeliveryOrderItemId).HasColumnName("delivery_order_item_id");
            entity.Property(x => x.DeliveryOrderId).HasColumnName("delivery_order_id");
            entity.Property(x => x.DeliveryProductId).HasColumnName("delivery_product_id");
            entity.Property(x => x.ProductName).HasColumnName("product_name");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price");
            entity.Property(x => x.Subtotal).HasColumnName("subtotal");
        });

        builder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("inventory_movement", table =>
            {
                table.HasCheckConstraint("ck_inventory_movement_quantity_delta", "quantity_delta <> 0");
                table.HasCheckConstraint("ck_inventory_movement_unit_cost", "unit_cost IS NULL OR unit_cost >= 0");
                table.HasCheckConstraint("ck_inventory_movement_type", "movement_type IN ('Entrada', 'Salida', 'Ajuste', 'Reserva', 'Liberación')");
            });
            entity.HasKey(x => x.InventoryMovementId);
            entity.Property(x => x.InventoryMovementId).HasColumnName("inventory_movement_id").HasColumnType("bigint").ValueGeneratedOnAdd();
            entity.Property(x => x.DeliveryProductId).HasColumnName("delivery_product_id");
            entity.Property(x => x.DeliveryOrderId).HasColumnName("delivery_order_id");
            entity.Property(x => x.PerformedByUserId).HasColumnName("performed_by_user_id");
            entity.Property(x => x.MovementType).HasColumnName("movement_type").HasMaxLength(20);
            entity.Property(x => x.QuantityDelta).HasColumnName("quantity_delta");
            entity.Property(x => x.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(12,2)");
            entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.DeliveryProductId, x.CreatedAt }).HasDatabaseName("ix_inventory_movement_product_created_at");
            entity.HasIndex(x => x.DeliveryOrderId).HasDatabaseName("ix_inventory_movement_order");
            entity.HasIndex(x => x.PerformedByUserId).HasDatabaseName("ix_inventory_movement_user");
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.DeliveryProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.DeliveryOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PerformedByUser).WithMany().HasForeignKey(x => x.PerformedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DeliveryIncident>(entity =>
        {
            entity.ToTable("delivery_incident", table =>
            {
                table.HasCheckConstraint("ck_delivery_incident_severity", "severity IN ('Baja', 'Media', 'Alta', 'Crítica')");
                table.HasCheckConstraint("ck_delivery_incident_status", "status IN ('Abierto', 'En revisión', 'Resuelto', 'Cerrado')");
                table.HasCheckConstraint("ck_delivery_incident_resolution", "resolved_at IS NULL OR resolved_at >= created_at");
            });
            entity.HasKey(x => x.DeliveryIncidentId);
            entity.Property(x => x.DeliveryIncidentId).HasColumnName("delivery_incident_id").HasColumnType("bigint").ValueGeneratedOnAdd();
            entity.Property(x => x.DeliveryOrderId).HasColumnName("delivery_order_id");
            entity.Property(x => x.ReportedByUserId).HasColumnName("reported_by_user_id");
            entity.Property(x => x.IncidentType).HasColumnName("incident_type").HasMaxLength(60);
            entity.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20).HasDefaultValue("Media");
            entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Abierto");
            entity.Property(x => x.DetailsJson).HasColumnName("details").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.DeliveryOrderId, x.Status }).HasDatabaseName("ix_delivery_incident_order_status");
            entity.HasIndex(x => x.ReportedByUserId).HasDatabaseName("ix_delivery_incident_reporter");
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_delivery_incident_created_at");
            entity.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.DeliveryOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ReportedByUser).WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_log");
            entity.HasKey(x => x.AuditLogId);
            entity.Property(x => x.AuditLogId).HasColumnName("audit_log_id").HasColumnType("bigint").ValueGeneratedOnAdd();
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(80);
            entity.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(120);
            entity.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(128);
            entity.Property(x => x.OldValuesJson).HasColumnName("old_values").HasColumnType("jsonb");
            entity.Property(x => x.NewValuesJson).HasColumnName("new_values").HasColumnType("jsonb");
            entity.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("ix_audit_log_entity");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_audit_log_user_created_at");
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_audit_log_created_at");
            entity.HasIndex(x => x.CorrelationId).HasDatabaseName("ix_audit_log_correlation_id");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OrderStatusHistory>(entity =>
        {
            entity.ToTable("order_status_history", table =>
            {
                table.HasCheckConstraint("ck_order_status_history_previous", "previous_status IS NULL OR previous_status IN ('Pendiente', 'En preparación', 'En camino', 'Entregado', 'Cancelado')");
                table.HasCheckConstraint("ck_order_status_history_new", "new_status IN ('Pendiente', 'En preparación', 'En camino', 'Entregado', 'Cancelado')");
            });
            entity.HasKey(x => x.OrderStatusHistoryId);
            entity.Property(x => x.OrderStatusHistoryId).HasColumnName("order_status_history_id").HasColumnType("bigint").ValueGeneratedOnAdd();
            entity.Property(x => x.DeliveryOrderId).HasColumnName("delivery_order_id");
            entity.Property(x => x.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.Property(x => x.PreviousStatus).HasColumnName("previous_status").HasMaxLength(30);
            entity.Property(x => x.NewStatus).HasColumnName("new_status").HasMaxLength(30);
            entity.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
            entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.ChangedAt).HasColumnName("changed_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.DeliveryOrderId, x.ChangedAt }).HasDatabaseName("ix_order_status_history_order_changed_at");
            entity.HasIndex(x => x.ChangedByUserId).HasDatabaseName("ix_order_status_history_user");
            entity.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.DeliveryOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StockReservation>(entity =>
        {
            entity.ToTable("stock_reservation", table =>
            {
                table.HasCheckConstraint("ck_stock_reservation_quantity", "quantity > 0");
                table.HasCheckConstraint("ck_stock_reservation_status", "status IN ('Activa', 'Confirmada', 'Liberada', 'Expirada', 'Cancelada')");
                table.HasCheckConstraint("ck_stock_reservation_expiration", "expires_at > created_at");
                table.HasCheckConstraint("ck_stock_reservation_release", "released_at IS NULL OR released_at >= created_at");
            });
            entity.HasKey(x => x.StockReservationId);
            entity.Property(x => x.StockReservationId).HasColumnName("stock_reservation_id").HasColumnType("bigint").ValueGeneratedOnAdd();
            entity.Property(x => x.DeliveryProductId).HasColumnName("delivery_product_id");
            entity.Property(x => x.DeliveryOrderId).HasColumnName("delivery_order_id");
            entity.Property(x => x.ReservedByUserId).HasColumnName("reserved_by_user_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Activa");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.Property(x => x.ReleasedAt).HasColumnName("released_at").HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.DeliveryOrderId, x.DeliveryProductId }).IsUnique().HasFilter("status = 'Activa'").HasDatabaseName("ux_stock_reservation_active_order_product");
            entity.HasIndex(x => new { x.Status, x.ExpiresAt }).HasDatabaseName("ix_stock_reservation_status_expires_at");
            entity.HasIndex(x => x.DeliveryProductId).HasDatabaseName("ix_stock_reservation_product");
            entity.HasIndex(x => x.ReservedByUserId).HasDatabaseName("ix_stock_reservation_user");
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.DeliveryProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.DeliveryOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ReservedByUser).WithMany().HasForeignKey(x => x.ReservedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<EmailQueueItem>(entity =>
        {
            entity.ToTable("email_queue", table =>
            {
                table.HasCheckConstraint("ck_email_queue_attempt_count", "attempt_count >= 0");
                table.HasCheckConstraint("ck_email_queue_max_attempts", "max_attempts > 0 AND attempt_count <= max_attempts");
                table.HasCheckConstraint("ck_email_queue_status", "status IN ('Pendiente', 'Procesando', 'Enviado', 'Fallido', 'Cancelado')");
            });
            entity.HasKey(x => x.EmailQueueId);
            entity.Property(x => x.EmailQueueId).HasColumnName("email_queue_id").HasColumnType("bigint").ValueGeneratedOnAdd();
            entity.Property(x => x.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(320);
            entity.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(255);
            entity.Property(x => x.BodyHtml).HasColumnName("body_html").HasColumnType("text");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Pendiente");
            entity.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
            entity.Property(x => x.MaxAttempts).HasColumnName("max_attempts").HasDefaultValue(5);
            entity.Property(x => x.ScheduledAt).HasColumnName("scheduled_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.SentAt).HasColumnName("sent_at").HasColumnType("timestamp with time zone");
            entity.Property(x => x.LastError).HasColumnName("last_error").HasColumnType("text");
            entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.Status, x.ScheduledAt }).HasDatabaseName("ix_email_queue_status_scheduled_at");
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_email_queue_created_at");
        });

        builder.Entity<AiConsumptionLog>(entity =>
        {
            entity.ToTable("ai_consumption_log", table =>
            {
                table.HasCheckConstraint("ck_ai_consumption_log_tokens", "prompt_tokens >= 0 AND completion_tokens >= 0 AND total_tokens >= 0");
                table.HasCheckConstraint("ck_ai_consumption_log_cost", "estimated_cost >= 0");
                table.HasCheckConstraint("ck_ai_consumption_log_duration", "duration_milliseconds >= 0");
            });
            entity.HasKey(x => x.AiConsumptionLogId);
            entity.Property(x => x.AiConsumptionLogId).HasColumnName("ai_consumption_log_id").HasColumnType("bigint").ValueGeneratedOnAdd();
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ModelName).HasColumnName("model_name").HasMaxLength(120);
            entity.Property(x => x.Operation).HasColumnName("operation").HasMaxLength(80);
            entity.Property(x => x.PromptTokens).HasColumnName("prompt_tokens");
            entity.Property(x => x.CompletionTokens).HasColumnName("completion_tokens");
            entity.Property(x => x.TotalTokens).HasColumnName("total_tokens");
            entity.Property(x => x.EstimatedCost).HasColumnName("estimated_cost").HasColumnType("numeric(14,6)");
            entity.Property(x => x.DurationMilliseconds).HasColumnName("duration_milliseconds");
            entity.Property(x => x.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_ai_consumption_log_user_created_at");
            entity.HasIndex(x => new { x.ModelName, x.CreatedAt }).HasDatabaseName("ix_ai_consumption_log_model_created_at");
            entity.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_ai_consumption_log_created_at");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
