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
        { "Pendiente", "En preparación", "En camino", "Entregado", "Cancelado" };

    private readonly ApplicationDbContext _context;

    public DeliveryController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _context.DeliveryProducts
            .AsNoTracking()
            .Include(product => product.Store)
            .Where(product => product.IsAvailable && product.Store.IsActive)
            .OrderBy(product => product.Store.Name)
            .ThenBy(product => product.Name)
            .ToListAsync();

        var addresses = new List<UserAddress>();
        if (User.IsInRole("Usuario") && CurrentUserId is string userId)
        {
            await EnsurePrimaryAddressAsync(userId);
            addresses = await _context.UserAddresses.AsNoTracking()
                .Where(address => address.IdentityUserId == userId)
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
        if (quantity < 1 || CurrentUserId is not string userId)
        {
            TempData["Error"] = "Indica una cantidad válida y selecciona una dirección guardada.";
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
    public async Task<IActionResult> MyOrders()
    {
        var email = User.Identity!.Name!;
        var orders = await OrderQuery()
            .Where(order => order.CustomerEmail == email)
            .ToListAsync();
        return View("Orders", orders);
    }

    [Authorize(Roles = "Repartidor")]
    public async Task<IActionResult> Deliveries()
    {
        var email = User.Identity!.Name!;
        var orders = await OrderQuery()
            .Where(order => order.DeliveryPersonEmail == email && order.Status != "Cancelado")
            .ToListAsync();
        return View("Orders", orders);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Admin(int page = 1)
    {
        ViewBag.Stores = await _context.DeliveryStores.OrderBy(store => store.Name).ToListAsync();
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
                (status != "En camino" && status != "Entregado"))
                return Forbid();
        }

        var previousStatus = order.Status;
        if (previousStatus == status)
            return RedirectToAction(User.IsInRole("Administrador") ? nameof(Admin) : nameof(Deliveries));

        order.Status = status;
        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            DeliveryOrderId = order.DeliveryOrderId,
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            PreviousStatus = previousStatus,
            NewStatus = status
        });
        await _context.SaveChangesAsync();
        return RedirectToAction(User.IsInRole("Administrador") ? nameof(Admin) : nameof(Deliveries));
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
        return RedirectToAction(nameof(Admin));
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
