using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SakilaApp.Models.Commerce;
using SakilaApp.Models.Delivery;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
    }
}
