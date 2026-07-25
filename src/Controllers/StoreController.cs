using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SakilaApp.Data;
using SakilaApp.Models;
using SakilaApp.Models.Commerce;
using SakilaApp.Settings;

namespace SakilaApp.Controllers;

[Authorize]
public class StoreController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PayPalSettings _payPalSettings;

    public StoreController(ApplicationDbContext context, IOptions<PayPalSettings> payPalSettings)
    {
        _context = context;
        _payPalSettings = payPalSettings.Value;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var query = _context.FilmStocks
            .Where(f => f.IsActive && f.Stock > 0)
            .OrderBy(f => f.Title);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * 5).Take(5).ToListAsync();

        var paginated = new PaginatedList<FilmStock>(items, total, page, 5);
        ViewData["PaginatedList"] = paginated;
        ViewData["TotalRegistros"] = total;

        return View(paginated);
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart(int filmStockId, int quantity)
    {
        if (quantity <= 0) quantity = 1;

        var userEmail = User.Identity?.Name ?? "usuario@local";
        var stock = await _context.FilmStocks.FindAsync(filmStockId);
        if (stock == null) return NotFound();

        if (quantity > stock.Stock)
        {
            return BadRequest(new { success = false, message = "No existe stock suficiente." });
        }

        var item = await _context.ShoppingCartItems
            .FirstOrDefaultAsync(c => c.UserEmail == userEmail && c.FilmStockId == filmStockId);

        if (item == null)
        {
            _context.ShoppingCartItems.Add(new ShoppingCartItem
            {
                UserEmail = userEmail,
                FilmStockId = filmStockId,
                Quantity = quantity
            });
        }
        else
        {
            item.Quantity += quantity;
        }

        await _context.SaveChangesAsync();

        var totalItems = await _context.ShoppingCartItems
            .Where(c => c.UserEmail == userEmail)
            .SumAsync(c => c.Quantity);

        return Json(new { success = true, totalItems });
    }

    public async Task<IActionResult> Cart()
    {
        var userEmail = User.Identity?.Name ?? "usuario@local";

        var items = await _context.ShoppingCartItems
            .Include(c => c.FilmStock)
            .Where(c => c.UserEmail == userEmail)
            .ToListAsync();

        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(string provider = "PayPhone")
    {
        var userEmail = User.Identity?.Name ?? "usuario@local";

        var cartItems = await _context.ShoppingCartItems
            .Include(c => c.FilmStock)
            .Where(c => c.UserEmail == userEmail)
            .ToListAsync();

        if (!cartItems.Any())
        {
            TempData["Error"] = "El carrito está vacío.";
            return RedirectToAction(nameof(Cart));
        }

        foreach (var item in cartItems)
        {
            if (item.Quantity > item.FilmStock.Stock)
            {
                TempData["Error"] = $"Stock insuficiente para {item.FilmStock.Title}.";
                return RedirectToAction(nameof(Cart));
            }
        }

        var order = new PurchaseOrder
        {
            UserEmail = userEmail,
            Status = "Pending"
        };

        foreach (var item in cartItems)
        {
            var subtotal = item.Quantity * item.FilmStock.UnitPrice;

            order.Details.Add(new PurchaseOrderDetail
            {
                FilmStockId = item.FilmStockId,
                FilmTitle = item.FilmStock.Title,
                Quantity = item.Quantity,
                UnitPrice = item.FilmStock.UnitPrice,
                Subtotal = subtotal
            });

            order.Total += subtotal;
        }

        _context.PurchaseOrders.Add(order);
        _context.ShoppingCartItems.RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        if (provider.Equals("PayPal", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("CreatePayPalOrder", "Payment", new { orderId = order.PurchaseOrderId });
        }

        return RedirectToAction("CreateLink", "Payment", new { orderId = order.PurchaseOrderId });
    }

    [HttpPost]
    public async Task<IActionResult> CheckoutPayPalButton()
    {
        var userEmail = User.Identity?.Name ?? "usuario@local";

        var cartItems = await _context.ShoppingCartItems
            .Include(c => c.FilmStock)
            .Where(c => c.UserEmail == userEmail)
            .ToListAsync();

        if (!cartItems.Any())
        {
            TempData["Error"] = "Cart is empty.";
            return RedirectToAction(nameof(Cart));
        }

        foreach (var item in cartItems)
        {
            if (item.Quantity > item.FilmStock.Stock)
            {
                TempData["Error"] = $"Insufficient stock for {item.FilmStock.Title}.";
                return RedirectToAction(nameof(Cart));
            }
        }

        var order = new PurchaseOrder
        {
            UserEmail = userEmail,
            Status = "Pending"
        };

        foreach (var item in cartItems)
        {
            var subtotal = item.Quantity * item.FilmStock.UnitPrice;

            order.Details.Add(new PurchaseOrderDetail
            {
                FilmStockId = item.FilmStockId,
                FilmTitle = item.FilmStock.Title,
                Quantity = item.Quantity,
                UnitPrice = item.FilmStock.UnitPrice,
                Subtotal = subtotal
            });

            order.Total += subtotal;
        }

        _context.PurchaseOrders.Add(order);
        _context.ShoppingCartItems.RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        return RedirectToAction("PayPalButton", "Payment", new { orderId = order.PurchaseOrderId });
    }
}