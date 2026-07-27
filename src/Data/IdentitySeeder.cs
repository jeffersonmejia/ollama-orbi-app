using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SakilaApp.Models.Delivery;
using SakilaApp.Models.Identity;
using SakilaApp.Models.Operations;

namespace SakilaApp.Data;

public static class IdentitySeeder
{
    private static readonly string[] ApplicationRoles =
        { "Administrador", "Vendedor", "Repartidor", "Usuario" };

    private static readonly SeedUser[] SeedUsers =
    {
        new("jefferson.mejia@orbi.com", "Admin123*", "Administrador", "Jefferson", "Mejía", "0912345675", "Av. Principal 101", "Calle 9 de Octubre", "09", "0901", "Frente al parque"),
        new("maria.lopez@orbi.com", "Vendedor123*", "Vendedor", "María", "López", "1712345671", "Av. Amazonas", "Calle Naciones Unidas", "17", "1701", null),
        new("carlos.perez@orbi.com", "Reparto123*", "Repartidor", "Carlos", "Pérez", "0923456784", "Av. Nicolás Lapentti", "Calle Loja", "09", "0907", null),
        new("ana.torres@orbi.com", "Usuario123*", "Usuario", "Ana", "Torres", "0123456782", "Av. de las Américas", "Calle del Batán", "01", "0101", "Casa esquinera")
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var role in ApplicationRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var obsoleteRoles = roleManager.Roles
            .Where(role => role.Name != null && !ApplicationRoles.Contains(role.Name))
            .ToList();

        foreach (var role in obsoleteRoles)
        {
            var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
            foreach (var user in usersInRole)
            {
                await userManager.RemoveFromRoleAsync(user, role.Name!);
            }

            await roleManager.DeleteAsync(role);
        }

        foreach (var userSeed in SeedUsers)
        {
            var user = await userManager.FindByEmailAsync(userSeed.Email);

            if (user == null)
            {
                user = new IdentityUser
                {
                    UserName = userSeed.Email,
                    Email = userSeed.Email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, userSeed.Password);
                if (!result.Succeeded) continue;
            }

            var currentRoles = await userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Where(role => role != userSeed.Role).ToArray();
            if (rolesToRemove.Length > 0)
            {
                await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            if (!await userManager.IsInRoleAsync(user, userSeed.Role))
            {
                await userManager.AddToRoleAsync(user, userSeed.Role);
            }

            var profile = await dbContext.UserProfiles
                .SingleOrDefaultAsync(item => item.IdentityUserId == user.Id);

            if (profile is null)
            {
                profile = new UserProfile { IdentityUserId = user.Id };
                dbContext.UserProfiles.Add(profile);
            }

            profile.FirstName = userSeed.FirstName;
            profile.LastName = userSeed.LastName;
            profile.Cedula = userSeed.Cedula;
            profile.AddressLine1 = userSeed.AddressLine1;
            profile.AddressLine2 = userSeed.AddressLine2;
            profile.ProvinceCode = userSeed.ProvinceCode;
            profile.CityCode = userSeed.CityCode;
            profile.Reference = userSeed.Reference;

            var primaryAddress = await dbContext.UserAddresses
                .SingleOrDefaultAsync(item => item.IdentityUserId == user.Id && item.IsDefault);
            if (primaryAddress is null)
            {
                primaryAddress = new UserAddress
                {
                    IdentityUserId = user.Id,
                    Label = "Casa",
                    IsDefault = true
                };
                dbContext.UserAddresses.Add(primaryAddress);
            }

            primaryAddress.AddressLine1 = userSeed.AddressLine1;
            primaryAddress.AddressLine2 = userSeed.AddressLine2;
            primaryAddress.ProvinceCode = userSeed.ProvinceCode;
            primaryAddress.CityCode = userSeed.CityCode;
            primaryAddress.Reference = userSeed.Reference;
            primaryAddress.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync();

        await SeedStoresAsync(dbContext);
        await SeedProductsAsync(dbContext, userManager);
        await SeedOrdersAsync(dbContext, userManager);
        await SeedIncidentsAsync(dbContext, userManager);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStoresAsync(ApplicationDbContext db)
    {
        if (await db.DeliveryStores.AnyAsync()) return;

        db.DeliveryStores.AddRange(
            new DeliveryStore { Name = "Mercado Popular", Category = "Alimentos", Address = "Av. 9 de Octubre 1234, Guayaquil", ProvinceCode = "09", CityCode = "0901", IsActive = true },
            new DeliveryStore { Name = "Tienda Gourmet", Category = "Gourmet", Address = "Av. Amazonas N36-50, Quito", ProvinceCode = "17", CityCode = "1701", IsActive = true },
            new DeliveryStore { Name = "Café Artesanal", Category = "Cafetería", Address = "Calle La Niña 456, Quito", ProvinceCode = "17", CityCode = "1701", IsActive = true }
        );
    }

    private static async Task SeedProductsAsync(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        if (await db.DeliveryProducts.AnyAsync()) return;

        var vendedor = await userManager.FindByEmailAsync("maria.lopez@orbi.com");
        var vendedorId = vendedor?.Id;

        var stores = await db.DeliveryStores.ToListAsync();
        var mercado = stores.FirstOrDefault(s => s.Name == "Mercado Popular");
        var gourmet = stores.FirstOrDefault(s => s.Name == "Tienda Gourmet");
        var cafe = stores.FirstOrDefault(s => s.Name == "Café Artesanal");

        if (mercado != null)
        {
            db.DeliveryProducts.AddRange(
                new DeliveryProduct { DeliveryStoreId = mercado.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Plátano Burro (kg)", Price = 0.80m, UnitCost = 0.45m, Stock = 200, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = mercado.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Arroz Superior (kg)", Price = 1.20m, UnitCost = 0.75m, Stock = 150, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = mercado.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Aceite de Palma (L)", Price = 3.50m, UnitCost = 2.10m, Stock = 80, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = mercado.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Azúcar Rubia (kg)", Price = 1.80m, UnitCost = 1.10m, Stock = 120, IsAvailable = true }
            );
        }

        if (gourmet != null)
        {
            db.DeliveryProducts.AddRange(
                new DeliveryProduct { DeliveryStoreId = gourmet.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Queso de Cabra (200g)", Price = 5.90m, UnitCost = 3.80m, Stock = 40, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = gourmet.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Mermada de Guayaba", Price = 4.20m, UnitCost = 2.50m, Stock = 35, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = gourmet.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Chocolate Artesanal", Price = 6.50m, UnitCost = 4.00m, Stock = 25, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = gourmet.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Miel de Abeja (250ml)", Price = 7.80m, UnitCost = 5.00m, Stock = 30, IsAvailable = true }
            );
        }

        if (cafe != null)
        {
            db.DeliveryProducts.AddRange(
                new DeliveryProduct { DeliveryStoreId = cafe.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Café Molido Tueste Medio", Price = 8.50m, UnitCost = 5.20m, Stock = 50, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = cafe.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Café en Grano Arábica", Price = 12.00m, UnitCost = 7.50m, Stock = 30, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = cafe.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Chocolate Caliente (porción)", Price = 3.00m, UnitCost = 1.50m, Stock = 100, IsAvailable = true },
                new DeliveryProduct { DeliveryStoreId = cafe.DeliveryStoreId, CreatedByUserId = vendedorId, Name = "Té de Canela (porción)", Price = 2.50m, UnitCost = 1.00m, Stock = 100, IsAvailable = true }
            );
        }
    }

    private static async Task SeedOrdersAsync(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        if (await db.DeliveryOrders.AnyAsync()) return;

        var stores = await db.DeliveryStores.ToListAsync();
        var products = await db.DeliveryProducts.ToListAsync();
        var mercado = stores.FirstOrDefault(s => s.Name == "Mercado Popular");
        var gourmet = stores.FirstOrDefault(s => s.Name == "Tienda Gourmet");

        if (mercado == null || gourmet == null) return;

        var vendedor = await userManager.FindByEmailAsync("maria.lopez@orbi.com");
        var vendedorId = vendedor?.Id;

        var mercadoProducts = products.Where(p => p.DeliveryStoreId == mercado.DeliveryStoreId).ToList();
        var gourmetProducts = products.Where(p => p.DeliveryStoreId == gourmet.DeliveryStoreId).ToList();

        if (mercadoProducts.Count == 0 || gourmetProducts.Count == 0) return;

        var orders = new List<DeliveryOrder>();

        var order1 = new DeliveryOrder
        {
            DeliveryStoreId = mercado.DeliveryStoreId,
            CustomerEmail = "ana.torres@orbi.com",
            DeliveryPersonEmail = "carlos.perez@orbi.com",
            DeliveryAddress = "Av. de las Américas y Calle del Batán, Cuenca",
            Status = "Entregado",
            Total = 0m,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        orders.Add(order1);

        var order2 = new DeliveryOrder
        {
            DeliveryStoreId = gourmet.DeliveryStoreId,
            CustomerEmail = "ana.torres@orbi.com",
            DeliveryPersonEmail = "carlos.perez@orbi.com",
            DeliveryAddress = "Av. de las Américas y Calle del Batán, Cuenca",
            Status = "En camino",
            Total = 0m,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        orders.Add(order2);

        var order3 = new DeliveryOrder
        {
            DeliveryStoreId = mercado.DeliveryStoreId,
            CustomerEmail = "ana.torres@orbi.com",
            DeliveryPersonEmail = "carlos.perez@orbi.com",
            DeliveryAddress = "Av. de las Américas y Calle del Batán, Cuenca",
            Status = "Pendiente",
            Total = 0m,
            CreatedAt = DateTime.UtcNow.AddHours(-6)
        };
        orders.Add(order3);

        var order4 = new DeliveryOrder
        {
            DeliveryStoreId = gourmet.DeliveryStoreId,
            CustomerEmail = "jefferson.mejia@orbi.com",
            DeliveryPersonEmail = "carlos.perez@orbi.com",
            DeliveryAddress = "Av. Principal 101, Guayaquil",
            Status = "Entregado",
            Total = 0m,
            CreatedAt = DateTime.UtcNow.AddDays(-7)
        };
        orders.Add(order4);

        var order5 = new DeliveryOrder
        {
            DeliveryStoreId = mercado.DeliveryStoreId,
            CustomerEmail = "jefferson.mejia@orbi.com",
            DeliveryPersonEmail = "carlos.perez@orbi.com",
            DeliveryAddress = "Av. Principal 101, Guayaquil",
            Status = "Cancelado",
            Total = 0m,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };
        orders.Add(order5);

        var order6 = new DeliveryOrder
        {
            DeliveryStoreId = gourmet.DeliveryStoreId,
            CustomerEmail = "ana.torres@orbi.com",
            DeliveryPersonEmail = null,
            DeliveryAddress = "Av. de las Américas y Calle del Batán, Cuenca",
            Status = "En preparacion",
            Total = 0m,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        orders.Add(order6);

        db.DeliveryOrders.AddRange(orders);
        await db.SaveChangesAsync();

        var orderItems = new List<DeliveryOrderItem>();

        var item1 = new DeliveryOrderItem { DeliveryOrderId = order1.DeliveryOrderId, DeliveryProductId = mercadoProducts[0].DeliveryProductId, ProductName = mercadoProducts[0].Name, Quantity = 3, UnitPrice = mercadoProducts[0].Price, Subtotal = 3 * mercadoProducts[0].Price };
        order1.Total += item1.Subtotal;
        orderItems.Add(item1);

        var item2 = new DeliveryOrderItem { DeliveryOrderId = order1.DeliveryOrderId, DeliveryProductId = mercadoProducts[1].DeliveryProductId, ProductName = mercadoProducts[1].Name, Quantity = 2, UnitPrice = mercadoProducts[1].Price, Subtotal = 2 * mercadoProducts[1].Price };
        order1.Total += item2.Subtotal;
        orderItems.Add(item2);

        var item3 = new DeliveryOrderItem { DeliveryOrderId = order2.DeliveryOrderId, DeliveryProductId = gourmetProducts[2].DeliveryProductId, ProductName = gourmetProducts[2].Name, Quantity = 2, UnitPrice = gourmetProducts[2].Price, Subtotal = 2 * gourmetProducts[2].Price };
        order2.Total += item3.Subtotal;
        orderItems.Add(item3);

        var item4 = new DeliveryOrderItem { DeliveryOrderId = order2.DeliveryOrderId, DeliveryProductId = gourmetProducts[0].DeliveryProductId, ProductName = gourmetProducts[0].Name, Quantity = 1, UnitPrice = gourmetProducts[0].Price, Subtotal = 1 * gourmetProducts[0].Price };
        order2.Total += item4.Subtotal;
        orderItems.Add(item4);

        var item5 = new DeliveryOrderItem { DeliveryOrderId = order3.DeliveryOrderId, DeliveryProductId = mercadoProducts[2].DeliveryProductId, ProductName = mercadoProducts[2].Name, Quantity = 1, UnitPrice = mercadoProducts[2].Price, Subtotal = 1 * mercadoProducts[2].Price };
        order3.Total += item5.Subtotal;
        orderItems.Add(item5);

        var item6 = new DeliveryOrderItem { DeliveryOrderId = order4.DeliveryOrderId, DeliveryProductId = gourmetProducts[3].DeliveryProductId, ProductName = gourmetProducts[3].Name, Quantity = 3, UnitPrice = gourmetProducts[3].Price, Subtotal = 3 * gourmetProducts[3].Price };
        order4.Total += item6.Subtotal;
        orderItems.Add(item6);

        var item7 = new DeliveryOrderItem { DeliveryOrderId = order5.DeliveryOrderId, DeliveryProductId = mercadoProducts[3].DeliveryProductId, ProductName = mercadoProducts[3].Name, Quantity = 5, UnitPrice = mercadoProducts[3].Price, Subtotal = 5 * mercadoProducts[3].Price };
        order5.Total += item7.Subtotal;
        orderItems.Add(item7);

        var item8 = new DeliveryOrderItem { DeliveryOrderId = order6.DeliveryOrderId, DeliveryProductId = gourmetProducts[1].DeliveryProductId, ProductName = gourmetProducts[1].Name, Quantity = 2, UnitPrice = gourmetProducts[1].Price, Subtotal = 2 * gourmetProducts[1].Price };
        order6.Total += item8.Subtotal;
        orderItems.Add(item8);

        db.DeliveryOrderItems.AddRange(orderItems);
        await db.SaveChangesAsync();

        var payments = new List<DeliveryPayment>();
        var providers = new[] { "PayPhone", "PayPal" };

        foreach (var o in orders.Where(o => o.Status == "Entregado" || o.Status == "En camino"))
        {
            payments.Add(new DeliveryPayment
            {
                DeliveryOrderId = o.DeliveryOrderId,
                ExternalId = $"DEMO-{o.DeliveryOrderId:D4}-{Guid.NewGuid():N}".Substring(0, 80),
                Provider = providers[o.DeliveryOrderId % 2],
                Status = "Aprobado",
                Amount = o.Total,
                CreatedAt = o.CreatedAt,
                ConfirmedAt = o.CreatedAt.AddMinutes(2)
            });
        }

        payments.Add(new DeliveryPayment
        {
            DeliveryOrderId = order6.DeliveryOrderId,
            ExternalId = $"DEMO-{order6.DeliveryOrderId:D4}-{Guid.NewGuid():N}".Substring(0, 80),
            Provider = "PayPal",
            Status = "Pendiente",
            Amount = order6.Total,
            CreatedAt = order6.CreatedAt
        });

        db.DeliveryPayments.AddRange(payments);

        db.InventoryMovements.AddRange(
            new InventoryMovement { DeliveryProductId = mercadoProducts[0].DeliveryProductId, PerformedByUserId = vendedorId, MovementType = "Entrada", QuantityDelta = 200, UnitCost = mercadoProducts[0].UnitCost, CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) },
            new InventoryMovement { DeliveryProductId = mercadoProducts[1].DeliveryProductId, PerformedByUserId = vendedorId, MovementType = "Entrada", QuantityDelta = 150, UnitCost = mercadoProducts[1].UnitCost, CreatedAt = DateTimeOffset.UtcNow.AddDays(-10) },
            new InventoryMovement { DeliveryProductId = gourmetProducts[2].DeliveryProductId, PerformedByUserId = vendedorId, MovementType = "Salida", QuantityDelta = -2, UnitCost = gourmetProducts[2].UnitCost, Order = order2, CreatedAt = DateTimeOffset.UtcNow.AddDays(-2) },
            new InventoryMovement { DeliveryProductId = gourmetProducts[3].DeliveryProductId, PerformedByUserId = vendedorId, MovementType = "Salida", QuantityDelta = -3, UnitCost = gourmetProducts[3].UnitCost, Order = order4, CreatedAt = DateTimeOffset.UtcNow.AddDays(-7) }
        );
    }

    private static async Task SeedIncidentsAsync(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        if (await db.DeliveryIncidents.AnyAsync()) return;

        var order = await db.DeliveryOrders.FirstOrDefaultAsync(o => o.Status == "En camino");
        if (order == null) return;

        var repartidor = await userManager.FindByEmailAsync("carlos.perez@orbi.com");

        db.DeliveryIncidents.AddRange(
            new DeliveryIncident
            {
                DeliveryOrderId = order.DeliveryOrderId,
                ReportedByUserId = repartidor?.Id,
                IncidentType = "Retraso en ruta",
                Severity = "Media",
                Description = "Tráfico pesado en la vía principal, se estima 15 minutos de retraso.",
                Status = "Abierto",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
            },
            new DeliveryIncident
            {
                DeliveryOrderId = order.DeliveryOrderId,
                ReportedByUserId = repartidor?.Id,
                IncidentType = "Dirección incorrecta",
                Severity = "Baja",
                Description = "El cliente indicó una dirección diferente a la registrada en el pedido.",
                Status = "Resuelto",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                ResolvedAt = DateTimeOffset.UtcNow.AddHours(-18)
            }
        );
    }

    private sealed record SeedUser(
        string Email,
        string Password,
        string Role,
        string FirstName,
        string LastName,
        string Cedula,
        string AddressLine1,
        string AddressLine2,
        string ProvinceCode,
        string CityCode,
        string? Reference);
}
