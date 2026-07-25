using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SakilaApp.Data;
using SakilaApp.Models.Delivery;
using SakilaApp.Models.Operations;

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

        return View(products);
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder(int productId, int quantity, string deliveryAddress)
    {
        if (quantity < 1 || string.IsNullOrWhiteSpace(deliveryAddress))
        {
            TempData["Error"] = "Indica una cantidad y una dirección de entrega válidas.";
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
            DeliveryAddress = deliveryAddress.Trim(),
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
    public async Task<IActionResult> Admin()
    {
        ViewBag.Stores = await _context.DeliveryStores.OrderBy(store => store.Name).ToListAsync();
        return View(await OrderQuery().ToListAsync());
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
}
