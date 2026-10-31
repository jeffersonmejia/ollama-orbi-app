using OrbiApp.Application.Features.Shared.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Models;
using OrbiApp.Models.Commerce;

namespace OrbiApp.Controllers;

[Authorize]
public class StoreController : Controller
{
    private readonly IApplicationDataSession _context;

    public StoreController(IApplicationDataSession context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var query = _context.Set<Product>()
            .Where(f => f.IsActive && f.Stock > 0)
            .OrderBy(f => f.Name);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * 5).Take(5).ToListAsync();

        var paginated = new PaginatedList<Product>(items, total, page, 5);
        ViewData["PaginatedList"] = paginated;
        ViewData["TotalRegistros"] = total;

        return View(paginated);
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart(int productId, int quantity)
    {
        if (quantity <= 0) quantity = 1;

        var userEmail = User.Identity?.Name ?? "usuario@local";
        var stock = await _context.Set<Product>().FindAsync(productId);
        if (stock == null) return NotFound();

        if (quantity > stock.Stock)
        {
            return BadRequest(new { success = false, message = "No existe stock suficiente." });
        }

        var item = await _context.Set<ShoppingCartItem>()
            .FirstOrDefaultAsync(c => c.UserEmail == userEmail && c.ProductId == productId);

        if (item == null)
        {
            _context.Set<ShoppingCartItem>().Add(new ShoppingCartItem
            {
                UserEmail = userEmail,
                ProductId = productId,
                Quantity = quantity
            });
        }
        else
        {
            item.Quantity += quantity;
        }

        await _context.SaveChangesAsync();

        var totalItems = await _context.Set<ShoppingCartItem>()
            .Where(c => c.UserEmail == userEmail)
            .SumAsync(c => c.Quantity);

        return Json(new { success = true, totalItems });
    }

    public async Task<IActionResult> Cart(int page = 1)
    {
        var userEmail = User.Identity?.Name ?? "usuario@local";

        var query = _context.Set<ShoppingCartItem>()
            .Include(c => c.Product)
            .Where(c => c.UserEmail == userEmail)
            .OrderBy(c => c.ShoppingCartItemId);

        ViewData["CartTotal"] = await query.SumAsync(item => (decimal?)(item.Quantity * item.Product.UnitPrice)) ?? 0m;
        var items = await PaginatedList<ShoppingCartItem>.CreateAsync(query, Math.Max(1, page), 5);
        ViewData["PaginatedList"] = items;

        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> Checkout(string provider = "PayPhone")
    {
        var userEmail = User.Identity?.Name ?? "usuario@local";

        var cartItems = await _context.Set<ShoppingCartItem>()
            .Include(c => c.Product)
            .Where(c => c.UserEmail == userEmail)
            .ToListAsync();

        if (!cartItems.Any())
        {
            TempData["Error"] = "El carrito está vacío.";
            return RedirectToAction(nameof(Cart));
        }

        foreach (var item in cartItems)
        {
            if (item.Quantity > item.Product.Stock)
            {
                TempData["Error"] = $"Stock insuficiente para {item.Product.Name}.";
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
            var subtotal = item.Quantity * item.Product.UnitPrice;

            order.Details.Add(new PurchaseOrderDetail
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                Quantity = item.Quantity,
                UnitPrice = item.Product.UnitPrice,
                Subtotal = subtotal
            });

            order.Total += subtotal;
        }

        _context.Set<PurchaseOrder>().Add(order);
        _context.Set<ShoppingCartItem>().RemoveRange(cartItems);
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

        var cartItems = await _context.Set<ShoppingCartItem>()
            .Include(c => c.Product)
            .Where(c => c.UserEmail == userEmail)
            .ToListAsync();

        if (!cartItems.Any())
        {
            TempData["Error"] = "Cart is empty.";
            return RedirectToAction(nameof(Cart));
        }

        foreach (var item in cartItems)
        {
            if (item.Quantity > item.Product.Stock)
            {
                TempData["Error"] = $"Insufficient stock for {item.Product.Name}.";
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
            var subtotal = item.Quantity * item.Product.UnitPrice;

            order.Details.Add(new PurchaseOrderDetail
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                Quantity = item.Quantity,
                UnitPrice = item.Product.UnitPrice,
                Subtotal = subtotal
            });

            order.Total += subtotal;
        }

        _context.Set<PurchaseOrder>().Add(order);
        _context.Set<ShoppingCartItem>().RemoveRange(cartItems);
        await _context.SaveChangesAsync();

        return RedirectToAction("PayPalButton", "Payment", new { orderId = order.PurchaseOrderId });
    }
}
