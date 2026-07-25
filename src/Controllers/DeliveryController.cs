using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SakilaApp.Data;
using SakilaApp.Models.Delivery;
using SakilaApp.Models.Identity;
using SakilaApp.Models.Operations;
using SakilaApp.Models;

namespace SakilaApp.Controllers;

[Authorize]
public class DeliveryController : Controller
{
    private static readonly string[] ValidStatuses =
        { "Pendiente", "Comprado", "En preparación", "En camino", "Entregado", "Cancelado" };

    private readonly ApplicationDbContext _context;

    public DeliveryController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? buscar, decimal? precioMinimo, decimal? precioMaximo, string? categoria, int page = 1)
    {
        var productsQuery = _context.DeliveryProducts
            .AsNoTracking()
            .Include(product => product.Store).ThenInclude(store => store!.Province)
            .Include(product => product.Store).ThenInclude(store => store!.City)
            .Where(product => product.IsAvailable && product.Store.IsActive);

        string? userProvinceCode = null;
        string? userCityName = null;
        string? userCityCode = null;
        if (CurrentUserId is string userId)
        {
            var profile = await _context.UserProfiles.AsNoTracking()
                .Where(p => p.IdentityUserId == userId)
                .Include(p => p.Province)
                .Include(p => p.City)
                .FirstOrDefaultAsync();
            if (profile is not null)
            {
                userProvinceCode = profile.ProvinceCode;
                userCityName = profile.City?.Name;
                userCityCode = profile.CityCode;
                var hasCityStores = await _context.DeliveryProducts
                    .AnyAsync(p => p.IsAvailable && p.Store.IsActive && p.Store.CityCode == userCityCode);
                if (hasCityStores)
                {
                    productsQuery = productsQuery.Where(p => p.Store.CityCode == userCityCode);
                }
                else
                {
                    var hasProvinceStores = await _context.DeliveryProducts
                        .AnyAsync(p => p.IsAvailable && p.Store.IsActive && p.Store.ProvinceCode == userProvinceCode);
                    if (hasProvinceStores)
                        productsQuery = productsQuery.Where(p => p.Store.ProvinceCode == userProvinceCode);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim();
            var pattern = $"%{term}%";
            productsQuery = productsQuery.Where(product => EF.Functions.ILike(product.Name, pattern) ||
                EF.Functions.ILike(product.Store.Name, pattern) || EF.Functions.ILike(product.Store.Category, pattern));
        }
        if (precioMinimo.HasValue) productsQuery = productsQuery.Where(product => product.Price >= precioMinimo.Value);
        if (precioMaximo.HasValue) productsQuery = productsQuery.Where(product => product.Price <= precioMaximo.Value);
        if (!string.IsNullOrWhiteSpace(categoria)) productsQuery = productsQuery.Where(product => product.Store.Category == categoria);

        var products = await PaginatedList<DeliveryProduct>.CreateAsync(
            productsQuery.OrderBy(product => product.Store.Name).ThenBy(product => product.Name),
            Math.Max(1, page), 12);
        ViewBag.Buscar = buscar;
        ViewBag.PrecioMinimo = precioMinimo;
        ViewBag.PrecioMaximo = precioMaximo;
        ViewBag.Categoria = categoria;
        ViewBag.UserProvinceName = await _context.EcuadorProvinces.AsNoTracking()
            .Where(p => p.Code == userProvinceCode)
            .Select(p => p.Name)
            .FirstOrDefaultAsync();
        ViewBag.UserCityName = userCityName;
        ViewData["PaginatedList"] = products;

        var addresses = new List<UserAddress>();
        if (User.IsInRole("Usuario") && CurrentUserId is string userId2)
        {
            await EnsurePrimaryAddressAsync(userId2);
            addresses = await _context.UserAddresses.AsNoTracking()
                .Where(address => address.IdentityUserId == userId2)
                .Include(address => address.Province)
                .Include(address => address.City)
                .OrderByDescending(address => address.IsDefault)
                .ThenBy(address => address.Label)
                .ToListAsync();
        }

        return View(new DeliveryCatalogViewModel { Products = products, Addresses = addresses });
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder(int productId, int quantity)
    {
        if (quantity is < 1 or > 999 || CurrentUserId is not string userId)
        {
            TempData["Error"] = "La cantidad debe estar entre 1 y 999 y necesitas una dirección guardada.";
            return RedirectToAction(nameof(Index));
        }

        var deliveryAddress = await _context.UserAddresses.AsNoTracking()
            .Where(address => address.IdentityUserId == userId)
            .Include(address => address.Province)
            .Include(address => address.City)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.UserAddressId)
            .FirstOrDefaultAsync();
        if (deliveryAddress is null)
        {
            TempData["Error"] = "Agrega una dirección principal en tu perfil antes de realizar un pedido.";
            return RedirectToAction(nameof(Index));
        }

        var product = await _context.DeliveryProducts
            .Include(item => item.Store)
            .FirstOrDefaultAsync(item => item.DeliveryProductId == productId);

        if (product == null || !product.IsAvailable || !product.Store.IsActive)
            return NotFound();

        var subtotal = product.Price * quantity;
        var order = new DeliveryOrder
        {
            DeliveryStoreId = product.DeliveryStoreId,
            CustomerEmail = User.Identity!.Name!,
            DeliveryPersonEmail = "carlos.perez@orbi.com",
            DeliveryAddress = $"{deliveryAddress.Label}: {deliveryAddress.FormattedAddress}",
            Total = subtotal,
            Items = new List<DeliveryOrderItem>
            {
                new()
                {
                    DeliveryProductId = product.DeliveryProductId,
                    ProductName = product.Name,
                    Quantity = quantity,
                    UnitPrice = product.Price,
                    Subtotal = subtotal
                }
            }
        };

        _context.DeliveryOrders.Add(order);
        await _context.SaveChangesAsync();
        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            DeliveryOrderId = order.DeliveryOrderId,
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            NewStatus = order.Status,
            Note = "Pedido creado"
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Pedido #{order.DeliveryOrderId} creado correctamente.";
        return RedirectToAction(nameof(MyOrders));
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> MyOrders(string? buscar, int page = 1)
    {
        var email = User.Identity!.Name!;
        var query = OrderQuery().Where(order => order.CustomerEmail == email);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim();
            var pattern = $"%{term}%";
            var hasOrderId = int.TryParse(term.TrimStart('#'), out var orderId);
            query = query.Where(order => EF.Functions.ILike(order.Store.Name, pattern) ||
                EF.Functions.ILike(order.Status, pattern) ||
                order.Items.Any(item => EF.Functions.ILike(item.ProductName, pattern)) ||
                (hasOrderId && order.DeliveryOrderId == orderId));
        }
        var orders = await PaginatedList<DeliveryOrder>.CreateAsync(
            query, Math.Max(1, page), 5);
        ViewBag.Buscar = buscar;
        ViewData["PaginatedList"] = orders;
        return View("Orders", orders);
    }

    [Authorize(Roles = "Repartidor")]
    public async Task<IActionResult> Deliveries(string? buscar, int page = 1)
    {
        var email = User.Identity!.Name!;
        var query = OrderQuery().Where(order => order.DeliveryPersonEmail == email && order.Status != "Cancelado");
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim();
            var pattern = $"%{term}%";
            var hasOrderId = int.TryParse(term.TrimStart('#'), out var orderId);
            query = query.Where(order => EF.Functions.ILike(order.Store.Name, pattern) ||
                EF.Functions.ILike(order.Status, pattern) || EF.Functions.ILike(order.CustomerEmail, pattern) ||
                order.Items.Any(item => EF.Functions.ILike(item.ProductName, pattern)) ||
                (hasOrderId && order.DeliveryOrderId == orderId));
        }
        var orders = await PaginatedList<DeliveryOrder>.CreateAsync(
            query, Math.Max(1, page), 5);
        ViewBag.Buscar = buscar;
        ViewData["PaginatedList"] = orders;
        return View("Orders", orders);
    }

    [Authorize(Roles = "Vendedor")]
    public async Task<IActionResult> SellerInventory(string? buscar, int page = 1)
    {
        var userId = CurrentUserId;
        var query = _context.DeliveryProducts
            .AsNoTracking()
            .Include(p => p.Store)
            .Where(p => p.CreatedByUserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim();
            var pattern = $"%{term}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Store.Name, pattern));
        }

        var products = await PaginatedList<DeliveryProduct>.CreateAsync(query, Math.Max(1, page), 10);
        ViewBag.Buscar = buscar;
        ViewData["PaginatedList"] = products;
        ViewData["Stores"] = await _context.DeliveryStores.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        return View(products);
    }

    [HttpPost]
    [Authorize(Roles = "Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(string name, decimal price, decimal unitCost, int stock, int storeId)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(name) || price < 0 || unitCost < 0 || stock < 0 || stock > 999)
        {
            TempData["Error"] = "Completa todos los campos correctamente. Stock debe ser 0-999.";
            return RedirectToAction(nameof(SellerInventory));
        }

        var store = await _context.DeliveryStores.FindAsync(storeId);
        if (store == null)
        {
            TempData["Error"] = "La tienda seleccionada no existe.";
            return RedirectToAction(nameof(SellerInventory));
        }

        _context.DeliveryProducts.Add(new DeliveryProduct
        {
            DeliveryStoreId = storeId,
            CreatedByUserId = userId,
            Name = name.Trim(),
            Price = price,
            UnitCost = unitCost,
            Stock = stock,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Producto '{name.Trim()}' creado correctamente.";
        return RedirectToAction(nameof(SellerInventory));
    }

    [HttpPost]
    [Authorize(Roles = "Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProduct(int productId, string name, decimal price, decimal unitCost, int stock)
    {
        var userId = CurrentUserId;
        var product = await _context.DeliveryProducts.FirstOrDefaultAsync(p =>
            p.DeliveryProductId == productId && p.CreatedByUserId == userId);

        if (product == null)
        {
            TempData["Error"] = "Producto no encontrado o no tienes permiso para editarlo.";
            return RedirectToAction(nameof(SellerInventory));
        }

        if (string.IsNullOrWhiteSpace(name) || price < 0 || unitCost < 0 || stock < 0 || stock > 999)
        {
            TempData["Error"] = "Completa todos los campos correctamente.";
            return RedirectToAction(nameof(SellerInventory));
        }

        product.Name = name.Trim();
        product.Price = price;
        product.UnitCost = unitCost;
        product.Stock = stock;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Producto '{name.Trim()}' actualizado.";
        return RedirectToAction(nameof(SellerInventory));
    }

    [HttpPost]
    [Authorize(Roles = "Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int productId)
    {
        var userId = CurrentUserId;
        var product = await _context.DeliveryProducts.FirstOrDefaultAsync(p =>
            p.DeliveryProductId == productId && p.CreatedByUserId == userId);

        if (product == null)
        {
            TempData["Error"] = "Producto no encontrado o no tienes permiso para eliminarlo.";
            return RedirectToAction(nameof(SellerInventory));
        }

        _context.DeliveryProducts.Remove(product);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Producto '{product.Name}' eliminado.";
        return RedirectToAction(nameof(SellerInventory));
    }

    [Authorize(Roles = "Administrador")]
    public IActionResult Admin()
    {
        return RedirectToAction(nameof(AdminStores));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminStores(int page = 1)
    {
        var stores = await PaginatedList<DeliveryStore>.CreateAsync(
            _context.DeliveryStores.AsNoTracking().OrderBy(store => store.Name), Math.Max(1, page), 5);
        ViewData["PaginatedList"] = stores;
        return View(stores);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminUsers(string? buscar, int page = 1)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim().ToLower();
            var profileUserIds = await _context.UserProfiles
                .Where(p => p.FirstName.ToLower().Contains(term) || p.LastName.ToLower().Contains(term))
                .Select(p => p.IdentityUserId)
                .ToListAsync();
            query = query.Where(u =>
                u.Email!.ToLower().Contains(term) || profileUserIds.Contains(u.Id));
        }
        var users = await PaginatedList<Microsoft.AspNetCore.Identity.IdentityUser>.CreateAsync(
            query.OrderBy(u => u.Email), Math.Max(1, page), 5);
        ViewBag.Buscar = buscar;
        ViewData["PaginatedList"] = users;
        return View(users);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
        user.LockoutEnd = isLocked
            ? null
            : new DateTimeOffset(DateTime.UtcNow.AddYears(100));
        await _context.SaveChangesAsync();
        TempData["Success"] = $"{user.Email} ahora está {(isLocked ? "habilitado" : "deshabilitado")}.";
        return RedirectToAction(nameof(AdminUsers));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminOrders(int page = 1)
    {
        var orders = await PaginatedList<DeliveryOrder>.CreateAsync(OrderQuery(), Math.Max(1, page), 5);
        ViewData["PaginatedList"] = orders;
        return View(orders);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Repartidor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, string status)
    {
        if (!ValidStatuses.Contains(status)) return BadRequest();

        var order = await _context.DeliveryOrders.FindAsync(orderId);
        if (order == null) return NotFound();

        if (User.IsInRole("Repartidor"))
        {
            if (order.DeliveryPersonEmail != User.Identity!.Name ||
                (status != "Comprado" && status != "En camino" && status != "Entregado"))
                return Forbid();
        }

        var previousStatus = order.Status;
        if (previousStatus == status)
            return RedirectToAction(User.IsInRole("Administrador") ? nameof(AdminOrders) : nameof(Deliveries));

        order.Status = status;
        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            DeliveryOrderId = order.DeliveryOrderId,
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            PreviousStatus = previousStatus,
            NewStatus = status
        });
        await _context.SaveChangesAsync();
        return RedirectToAction(User.IsInRole("Administrador") ? nameof(AdminOrders) : nameof(Deliveries));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStore(int storeId, string name, string category, string address)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(address))
        {
            TempData["Error"] = "Completa el nombre, la categoría y la dirección de la tienda.";
            return RedirectToAction(nameof(AdminStores));
        }

        var store = await _context.DeliveryStores.FindAsync(storeId);
        if (store == null) return NotFound();

        store.Name = name.Trim();
        store.Category = category.Trim();
        store.Address = address.Trim();
        await _context.SaveChangesAsync();
        TempData["Success"] = $"La tienda {store.Name} fue actualizada.";
        return RedirectToAction(nameof(AdminStores));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStore(int storeId)
    {
        var store = await _context.DeliveryStores.FindAsync(storeId);
        if (store == null) return NotFound();

        store.IsActive = !store.IsActive;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"{store.Name} ahora está {(store.IsActive ? "activa" : "inactiva")}.";
        return RedirectToAction(nameof(AdminStores));
    }

    private IQueryable<DeliveryOrder> OrderQuery() => _context.DeliveryOrders
        .AsNoTracking()
        .Include(order => order.Store)
        .Include(order => order.Items)
        .OrderByDescending(order => order.CreatedAt);

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task EnsurePrimaryAddressAsync(string userId)
    {
        if (await _context.UserAddresses.AnyAsync(address => address.IdentityUserId == userId)) return;

        var profile = await _context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.IdentityUserId == userId);
        if (profile is null || string.IsNullOrWhiteSpace(profile.AddressLine1)) return;

        _context.UserAddresses.Add(new UserAddress
        {
            IdentityUserId = userId,
            Label = "Casa",
            AddressLine1 = profile.AddressLine1,
            AddressLine2 = profile.AddressLine2,
            ProvinceCode = profile.ProvinceCode,
            CityCode = profile.CityCode,
            Reference = profile.Reference,
            IsDefault = true
        });
        await _context.SaveChangesAsync();
    }
}
