using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SakilaApp.Data;
using SakilaApp.Models.Delivery;
using SakilaApp.Models.Identity;
using SakilaApp.Models.Operations;
using SakilaApp.Models;
using SakilaApp.Services;
using SakilaApp.Services.Payments;
using SakilaApp.Settings;
using Microsoft.Extensions.Options;

namespace SakilaApp.Controllers;

[Authorize]
public class DeliveryController : Controller
{
    private static readonly string[] ValidStatuses =
        { "Pendiente", "Comprado", "En preparación", "En camino", "Entregado", "Cancelado" };

    private readonly ApplicationDbContext _context;
    private readonly PayPalService _payPalService;
    private readonly PayPhoneApiLinkService _payPhoneService;
    private readonly ExternalMarketPriceService _marketPriceService;

    public DeliveryController(
        ApplicationDbContext context,
        PayPalService payPalService,
        PayPhoneApiLinkService payPhoneService,
        ExternalMarketPriceService marketPriceService)
    {
        _context = context;
        _payPalService = payPalService;
        _payPhoneService = payPhoneService;
        _marketPriceService = marketPriceService;
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
                productsQuery = productsQuery.Where(p => p.Store.CityCode == userCityCode);
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
    public async Task<IActionResult> AddToCart(int productId, int quantity)
    {
        if (quantity is < 1 or > 999 || CurrentUserId is not string userId)
        {
            TempData["Error"] = "Cantidad inválida.";
            return RedirectToAction(nameof(Index));
        }

        var product = await _context.DeliveryProducts
            .Include(p => p.Store)
            .FirstOrDefaultAsync(p => p.DeliveryProductId == productId);

        if (product == null || !product.IsAvailable || !product.Store.IsActive)
        {
            TempData["Error"] = "Producto no disponible.";
            return RedirectToAction(nameof(Index));
        }

        var email = User.Identity!.Name!;
        var existing = await _context.DeliveryCartItems
            .FirstOrDefaultAsync(c => c.UserEmail == email && c.DeliveryProductId == productId);

        if (existing != null)
        {
            existing.Quantity = Math.Min(999, existing.Quantity + quantity);
        }
        else
        {
            _context.DeliveryCartItems.Add(new DeliveryCartItem
            {
                UserEmail = email,
                DeliveryProductId = productId,
                Quantity = quantity,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = $"{product.Name} agregado al carrito.";
        return RedirectToAction(nameof(Cart));
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> Cart()
    {
        var email = User.Identity!.Name!;
        var items = await _context.DeliveryCartItems
            .AsNoTracking()
            .Include(c => c.Product).ThenInclude(p => p.Store)
            .Where(c => c.UserEmail == email)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var total = items.Sum(c => c.Product.Price * c.Quantity);
        ViewBag.CartTotal = total;

        var hasAddress = await _context.UserAddresses.AnyAsync(a => a.IdentityUserId == CurrentUserId);
        ViewBag.HasAddress = hasAddress;

        return View(items);
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCartItem(long cartItemId, int quantity)
    {
        var email = User.Identity!.Name!;
        var item = await _context.DeliveryCartItems
            .FirstOrDefaultAsync(c => c.DeliveryCartItemId == cartItemId && c.UserEmail == email);

        if (item == null) return RedirectToAction(nameof(Cart));

        if (quantity < 1)
            _context.DeliveryCartItems.Remove(item);
        else
            item.Quantity = Math.Min(999, quantity);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Cart));
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromCart(long cartItemId)
    {
        var email = User.Identity!.Name!;
        var item = await _context.DeliveryCartItems
            .FirstOrDefaultAsync(c => c.DeliveryCartItemId == cartItemId && c.UserEmail == email);

        if (item != null)
        {
            _context.DeliveryCartItems.Remove(item);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Cart));
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string provider)
    {
        if (provider is not ("PayPal" or "PayPhone"))
        {
            TempData["Error"] = "Proveedor de pago no válido.";
            return RedirectToAction(nameof(Cart));
        }

        var email = User.Identity!.Name!;
        var userId = CurrentUserId;

        var items = await _context.DeliveryCartItems
            .Include(c => c.Product).ThenInclude(p => p.Store)
            .Where(c => c.UserEmail == email)
            .ToListAsync();

        if (!items.Any())
        {
            TempData["Error"] = "El carrito está vacío.";
            return RedirectToAction(nameof(Cart));
        }

        var hasAddress = await _context.UserAddresses.AnyAsync(a => a.IdentityUserId == userId);
        if (!hasAddress)
        {
            TempData["Error"] = "Agrega una dirección en tu perfil antes de pagar.";
            return RedirectToAction(nameof(Cart));
        }

        var deliveryAddress = await _context.UserAddresses
            .Where(a => a.IdentityUserId == userId)
            .Include(a => a.Province).Include(a => a.City)
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.UserAddressId)
            .FirstOrDefaultAsync();

        var rawAddress = deliveryAddress != null
            ? $"{deliveryAddress.Label}: {deliveryAddress.FormattedAddress}"
            : "Sin dirección";
        var addressText = rawAddress.Length <= 180 ? rawAddress : rawAddress.Substring(0, 180);

        var groups = items.GroupBy(c => c.Product.DeliveryStoreId);
        var createdOrderIds = new List<int>();
        decimal grandTotal = 0;

        foreach (var group in groups)
        {
            decimal total = 0;
            var orderItems = new List<DeliveryOrderItem>();
            foreach (var cartItem in group)
            {
                var subtotal = cartItem.Product.Price * cartItem.Quantity;
                total += subtotal;
                orderItems.Add(new DeliveryOrderItem
                {
                    DeliveryProductId = cartItem.Product.DeliveryProductId,
                    ProductName = cartItem.Product.Name.Length <= 100 ? cartItem.Product.Name : cartItem.Product.Name[..100],
                    Quantity = cartItem.Quantity,
                    UnitPrice = cartItem.Product.Price,
                    Subtotal = subtotal
                });
            }

            var order = new DeliveryOrder
            {
                DeliveryStoreId = group.Key,
                CustomerEmail = email,
                DeliveryAddress = addressText,
                Status = "Pendiente",
                Total = total,
                Items = orderItems
            };
            _context.DeliveryOrders.Add(order);
            await _context.SaveChangesAsync();

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                DeliveryOrderId = order.DeliveryOrderId,
                ChangedByUserId = userId,
                NewStatus = "Pendiente",
                Note = $"Pedido creado desde carrito — pago: {provider}"
            });

            _context.DeliveryPayments.Add(new DeliveryPayment
            {
                DeliveryOrderId = order.DeliveryOrderId,
                ExternalId = $"PENDING-{Guid.NewGuid():N}",
                Provider = provider,
                Status = "Pendiente",
                Amount = total,
                CreatedAt = DateTimeOffset.UtcNow
            });

            createdOrderIds.Add(order.DeliveryOrderId);
            grandTotal += total;
        }

        await _context.SaveChangesAsync();

        if (grandTotal < 1.00m)
        {
            _context.DeliveryCartItems.RemoveRange(items);
            await _context.SaveChangesAsync();
            TempData["Error"] = "El total mínimo para pagar es $1.00.";
            return RedirectToAction(nameof(Cart));
        }

        var firstOrderId = createdOrderIds.First();
        var firstPayment = await _context.DeliveryPayments
            .FirstOrDefaultAsync(p => p.DeliveryOrderId == firstOrderId);

        string paymentUrl;

        if (provider == "PayPal")
        {
            string reference = $"Orbi Order #{string.Join(",", createdOrderIds)}";
            try
            {
                var returnUrl = $"{Request.Scheme}://{Request.Host}/Delivery/PaymentSuccess";
                var cancelUrl = $"{Request.Scheme}://{Request.Host}/Delivery/PaymentCancel";
                var result = await _payPalService.CreateOrderAsync(grandTotal, reference, returnUrl, cancelUrl);

                if (firstPayment != null)
                {
                    firstPayment.ExternalId = result.OrderId;
                    await _context.SaveChangesAsync();
                }

                paymentUrl = result.ApprovalUrl;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al conectar con PayPal: {ex.Message}";
                return RedirectToAction(nameof(Cart));
            }
        }
        else
        {
            string clientTransactionId = DateTime.Now.ToString("yyMMddHHmmssfff")[..15];
            string reference = $"Orbi Order #{string.Join(",", createdOrderIds)}";
            try
            {
                var link = await _payPhoneService.CreatePaymentLinkAsync(
                    grandTotal, clientTransactionId, reference);

                if (firstPayment != null)
                {
                    firstPayment.ExternalId = clientTransactionId;
                    await _context.SaveChangesAsync();
                }

                paymentUrl = link;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al conectar con PayPhone: {ex.Message}";
                return RedirectToAction(nameof(Cart));
            }
        }

        _context.DeliveryCartItems.RemoveRange(items);
        await _context.SaveChangesAsync();

        return Redirect(paymentUrl);
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> PaymentSuccess(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "PayPal no devolvió un token válido.";
            return RedirectToAction(nameof(MyOrders));
        }

        var payment = await _context.DeliveryPayments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Provider == "PayPal" && p.ExternalId == token);

        if (payment == null)
        {
            TempData["Error"] = "Pago no encontrado.";
            return RedirectToAction(nameof(MyOrders));
        }

        if (payment.Status == "Aprobado")
        {
            TempData["Success"] = "El pago ya fue procesado.";
            return RedirectToAction(nameof(MyOrders));
        }

        try
        {
            var capture = await _payPalService.CaptureOrderAsync(token);

            payment.Status = capture.Status == "COMPLETED" ? "Aprobado" : capture.Status;
            payment.ExternalId = capture.CaptureId;
            payment.ConfirmedAt = DateTimeOffset.UtcNow;

            if (capture.Status == "COMPLETED")
            {
                var orders = await _context.DeliveryOrders
                    .Where(o => payment.DeliveryOrderId == o.DeliveryOrderId)
                    .ToListAsync();
                foreach (var o in orders)
                    o.Status = "Comprado";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "¡Pago con PayPal completado exitosamente!";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al confirmar PayPal: {ex.Message}";
        }

        return RedirectToAction(nameof(MyOrders));
    }

    [Authorize(Roles = "Usuario")]
    public IActionResult PaymentCancel()
    {
        TempData["Error"] = "El pago con PayPal fue cancelado.";
        return RedirectToAction(nameof(MyOrders));
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> PaymentPayPhoneCallback(string? clientTransactionId)
    {
        if (string.IsNullOrWhiteSpace(clientTransactionId))
        {
            TempData["Error"] = "PayPhone no devolvió referencia válida.";
            return RedirectToAction(nameof(MyOrders));
        }

        var payment = await _context.DeliveryPayments
            .FirstOrDefaultAsync(p => p.Provider == "PayPhone" && p.ExternalId == clientTransactionId);

        if (payment != null)
        {
            payment.Status = "Aprobado";
            payment.ConfirmedAt = DateTimeOffset.UtcNow;

            var order = await _context.DeliveryOrders.FindAsync(payment.DeliveryOrderId);
            if (order != null) order.Status = "Comprado";

            await _context.SaveChangesAsync();
            TempData["Success"] = "¡Pago con PayPhone completado exitosamente!";
        }
        else
        {
            TempData["Error"] = "Pago PayPhone no encontrado.";
        }

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
    public async Task<IActionResult> SellerStore()
    {
        var userId = CurrentUserId;
        var store = await _context.DeliveryStores.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

        var profile = await _context.UserProfiles.AsNoTracking()
            .Include(p => p.Province).Include(p => p.City)
            .FirstOrDefaultAsync(p => p.IdentityUserId == userId);
        ViewData["Profile"] = profile;

        return View(store);
    }

    [HttpPost]
    [Authorize(Roles = "Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SellerStore(string name, string category)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
        {
            TempData["Error"] = "Completa nombre y categoría.";
            return RedirectToAction(nameof(SellerStore));
        }

        var profile = await _context.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdentityUserId == userId);
        if (profile == null)
        {
            TempData["Error"] = "No se encontró tu perfil de usuario.";
            return RedirectToAction(nameof(SellerStore));
        }

        var store = await _context.DeliveryStores.FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        if (store == null)
        {
            store = new DeliveryStore
            {
                Name = name.Trim(),
                Category = category.Trim(),
                Address = profile.AddressLine1 + (string.IsNullOrWhiteSpace(profile.AddressLine2) ? "" : " " + profile.AddressLine2),
                ProvinceCode = profile.ProvinceCode,
                CityCode = profile.CityCode,
                OwnerUserId = userId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.DeliveryStores.Add(store);
        }
        else
        {
            store.Name = name.Trim();
            store.Category = category.Trim();
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Tu tienda fue guardada correctamente.";
        return RedirectToAction(nameof(SellerInventory));
    }

    [Authorize(Roles = "Vendedor")]
    public async Task<IActionResult> SellerInventory(string? buscar, decimal? precioMinimo, decimal? precioMaximo, int page = 1)
    {
        var userId = CurrentUserId;
        var myStore = await _context.DeliveryStores.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

        if (myStore == null)
        {
            TempData["Error"] = "Primero debes registrar tu tienda.";
            return RedirectToAction(nameof(SellerStore));
        }

        var query = _context.DeliveryProducts
            .AsNoTracking()
            .Include(p => p.Store)
            .Where(p => p.DeliveryStoreId == myStore.DeliveryStoreId)
            .OrderByDescending(p => p.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim();
            var pattern = $"%{term}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern));
        }
        if (precioMinimo.HasValue) query = query.Where(p => p.Price >= precioMinimo.Value);
        if (precioMaximo.HasValue) query = query.Where(p => p.Price <= precioMaximo.Value);

        var products = await PaginatedList<DeliveryProduct>.CreateAsync(query, Math.Max(1, page), 5);
        ViewBag.Buscar = buscar;
        ViewBag.PrecioMinimo = precioMinimo;
        ViewBag.PrecioMaximo = precioMaximo;
        ViewBag.MyStore = myStore;
        ViewData["PaginatedList"] = products;
        return View(products);
    }

    [HttpPost]
    [Authorize(Roles = "Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(string name, decimal price, decimal unitCost, int stock)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(name) || price < 0 || unitCost < 0 || stock < 0 || stock > 999)
        {
            TempData["Error"] = "Completa todos los campos correctamente. Stock debe ser 0-999.";
            return RedirectToAction(nameof(SellerInventory));
        }

        var store = await _context.DeliveryStores.FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        if (store == null)
        {
            TempData["Error"] = "Primero debes registrar tu tienda.";
            return RedirectToAction(nameof(SellerStore));
        }

        _context.DeliveryProducts.Add(new DeliveryProduct
        {
            DeliveryStoreId = store.DeliveryStoreId,
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
    public async Task<IActionResult> SuggestPrice(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return Json(new { ok = false, message = "Escribe el nombre del producto." });

        var userId = CurrentUserId;
        var profile = await _context.UserProfiles.AsNoTracking()
            .Include(p => p.Province).Include(p => p.City)
            .FirstOrDefaultAsync(p => p.IdentityUserId == userId);

        var provinceName = profile?.Province?.Name ?? "Guayaquil";
        var cityName = profile?.City?.Name ?? "Guayaquil";

        try
        {
            var analysis = await _marketPriceService.AnalyzeAsync(productName.Trim(), HttpContext.RequestAborted);
            if (analysis is null)
                return Json(new
                {
                    ok = false,
                    message = "No se encontraron precios públicos comparables en las fuentes externas consultadas."
                });

            return Json(new
            {
                ok = true,
                price = analysis.SuggestedPrice,
                minimumPrice = analysis.MinimumPrice,
                maximumPrice = analysis.MaximumPrice,
                province = provinceName,
                city = cityName,
                reason = $"Se tomó la mediana de {analysis.Sources.Count} precios públicos comparables para reducir el efecto de ofertas o valores atípicos.",
                disclaimer = "Las marcas, presentaciones y existencias pueden variar. Confirma el precio y el stock para Quito antes de publicar.",
                sources = analysis.Sources.Select(source => new
                {
                    store = source.Store,
                    product = source.Product,
                    price = source.Price,
                    url = source.Url
                })
            });
        }
        catch (Exception ex)
        {
            return Json(new { ok = false, message = "Error al conectar con la IA: " + ex.Message });
        }
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
    public async Task<IActionResult> AdminStores(string? buscar, string? provincia, string? ciudad, int page = 1)
    {
        var query = _context.DeliveryStores.AsNoTracking()
            .Include(s => s.Province).Include(s => s.City).AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(s => EF.Functions.ILike(s.Name, term) || EF.Functions.ILike(s.Category, term) || EF.Functions.ILike(s.Address, term));
        }
        if (!string.IsNullOrWhiteSpace(provincia))
            query = query.Where(s => s.ProvinceCode == provincia);
        if (!string.IsNullOrWhiteSpace(ciudad))
            query = query.Where(s => s.CityCode == ciudad);

        ViewBag.Buscar = buscar;
        ViewBag.Provincia = provincia;
        ViewBag.Ciudad = ciudad;
        ViewBag.Provincias = await _context.EcuadorProvinces.OrderBy(p => p.Name).ToListAsync();
        ViewBag.Ciudades = await _context.EcuadorCities
            .Where(c => string.IsNullOrWhiteSpace(provincia) || c.ProvinceCode == provincia)
            .OrderBy(c => c.Name).ToListAsync();

        var stores = await PaginatedList<DeliveryStore>.CreateAsync(
            query.OrderBy(s => s.Name), Math.Max(1, page), 5);
        var ownerIds = stores
            .Where(store => !string.IsNullOrWhiteSpace(store.OwnerUserId))
            .Select(store => store.OwnerUserId!)
            .Distinct()
            .ToList();
        var ownerProfiles = await _context.UserProfiles.AsNoTracking()
            .Where(profile => ownerIds.Contains(profile.IdentityUserId))
            .ToDictionaryAsync(profile => profile.IdentityUserId);
        var ownerEmails = await _context.Users.AsNoTracking()
            .Where(user => ownerIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Email ?? "Usuario sin correo");
        var ownerDetails = stores
            .Where(store => store.OwnerUserId is { Length: > 0 })
            .Select(store =>
            {
                var profile = ownerProfiles.GetValueOrDefault(store.OwnerUserId!);
                return new KeyValuePair<int, StoreOwnerOption>(
                    store.DeliveryStoreId,
                    new StoreOwnerOption
                    {
                        UserId = store.OwnerUserId!,
                        FullName = profile is null
                            ? ownerEmails.GetValueOrDefault(store.OwnerUserId!, "Propietario sin perfil")
                            : $"{profile.FirstName} {profile.LastName}".Trim(),
                        Email = ownerEmails.GetValueOrDefault(store.OwnerUserId!, "Usuario sin correo"),
                        MemberSince = profile?.CreatedAt ?? default,
                        StoreId = store.DeliveryStoreId
                    });
            })
            .ToDictionary(item => item.Key, item => item.Value);
        ViewBag.StoreOwnerDetails = ownerDetails;
        ViewBag.StoreOwners = stores.ToDictionary(
            store => store.DeliveryStoreId,
            store => ownerDetails.GetValueOrDefault(store.DeliveryStoreId)?.FullName ?? "Sin asignar");
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

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminAuditLog(string? buscar, int page = 1)
    {
        var query = _context.AuditLogs.AsNoTracking().Include(a => a.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(a => EF.Functions.ILike(a.Action, term) ||
                EF.Functions.ILike(a.EntityType, term) ||
                (a.EntityId != null && EF.Functions.ILike(a.EntityId, term)) ||
                (a.User != null && EF.Functions.ILike(a.User.Email!, term)));
        }
        ViewBag.Buscar = buscar;
        var logs = await PaginatedList<AuditLog>.CreateAsync(
            query.OrderByDescending(a => a.CreatedAt), Math.Max(1, page), 12);
        var userIds = logs
            .Where(log => !string.IsNullOrWhiteSpace(log.UserId))
            .Select(log => log.UserId!)
            .Distinct()
            .ToList();
        var userRolePairs = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync();
        ViewBag.UserRoles = userRolePairs
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => string.Join(", ", group.Select(item => item.RoleName).OrderBy(name => name)));
        ViewData["PaginatedList"] = logs;
        return View(logs);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminInventory(string? buscar, int page = 1)
    {
        var query = _context.InventoryMovements.AsNoTracking()
            .Include(m => m.Product).ThenInclude(p => p.Store)
            .Include(m => m.Order)
            .Include(m => m.PerformedByUser)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(m => EF.Functions.ILike(m.MovementType, term) ||
                EF.Functions.ILike(m.Product.Name, term) ||
                EF.Functions.ILike(m.Product.Store.Name, term) ||
                (m.PerformedByUser != null && EF.Functions.ILike(m.PerformedByUser.Email!, term)));
        }
        ViewBag.Buscar = buscar;
        var movements = await PaginatedList<InventoryMovement>.CreateAsync(
            query.OrderByDescending(m => m.CreatedAt), Math.Max(1, page), 12);
        ViewData["PaginatedList"] = movements;
        return View(movements);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminPayments(string? buscar, int page = 1)
    {
        var query = _context.DeliveryPayments.AsNoTracking()
            .Include(p => p.Order).ThenInclude(o => o.Store)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Provider, term) ||
                EF.Functions.ILike(p.Status, term) ||
                EF.Functions.ILike(p.ExternalId, term) ||
                EF.Functions.ILike(p.Order.CustomerEmail, term));
        }
        ViewBag.Buscar = buscar;
        var payments = await PaginatedList<DeliveryPayment>.CreateAsync(
            query.OrderByDescending(p => p.CreatedAt), Math.Max(1, page), 12);
        ViewData["PaginatedList"] = payments;
        return View(payments);
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> MyPayments(string? buscar, int page = 1)
    {
        var email = User.Identity!.Name!;
        var query = _context.DeliveryPayments.AsNoTracking()
            .Include(p => p.Order).ThenInclude(o => o.Store)
            .Where(p => p.Order.CustomerEmail == email)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Provider, term) ||
                EF.Functions.ILike(p.Status, term) ||
                EF.Functions.ILike(p.Order.Store.Name, term));
        }
        ViewBag.Buscar = buscar;
        var payments = await PaginatedList<DeliveryPayment>.CreateAsync(
            query.OrderByDescending(p => p.CreatedAt), Math.Max(1, page), 12);
        ViewData["PaginatedList"] = payments;
        return View(payments);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminLocations(string? buscar, int page = 1)
    {
        var query = _context.EcuadorCities.AsNoTracking()
            .Include(c => c.Province)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, term) || EF.Functions.ILike(c.Province.Name, term));
        }
        ViewBag.Buscar = buscar;
        ViewBag.Provincias = await _context.EcuadorProvinces.OrderBy(p => p.Name).ToListAsync();
        var cities = await PaginatedList<EcuadorCity>.CreateAsync(
            query.OrderBy(c => c.Province.Name).ThenBy(c => c.Name), Math.Max(1, page), 12);
        ViewData["PaginatedList"] = cities;
        return View(cities);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCity(string code, string provinceCode, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(provinceCode) || string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Completa código, provincia y nombre.";
            return RedirectToAction(nameof(AdminLocations));
        }
        if (await _context.EcuadorCities.AnyAsync(c => c.Code == code.Trim()))
        {
            TempData["Error"] = $"Ya existe una ciudad con código {code.Trim()}.";
            return RedirectToAction(nameof(AdminLocations));
        }
        _context.EcuadorCities.Add(new EcuadorCity { Code = code.Trim(), ProvinceCode = provinceCode.Trim(), Name = name.Trim() });
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Ciudad '{name.Trim()}' creada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCity(string code, string name)
    {
        var city = await _context.EcuadorCities.FindAsync(code);
        if (city == null) return NotFound();
        city.Name = name.Trim();
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Ciudad '{name.Trim()}' actualizada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCity(string code)
    {
        var city = await _context.EcuadorCities.FindAsync(code);
        if (city == null) return NotFound();
        _context.EcuadorCities.Remove(city);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Ciudad '{city.Name}' eliminada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProvince(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Completa código y nombre.";
            return RedirectToAction(nameof(AdminLocations));
        }
        if (await _context.EcuadorProvinces.AnyAsync(p => p.Code == code.Trim()))
        {
            TempData["Error"] = $"Ya existe una provincia con código {code.Trim()}.";
            return RedirectToAction(nameof(AdminLocations));
        }
        _context.EcuadorProvinces.Add(new EcuadorProvince { Code = code.Trim(), Name = name.Trim() });
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Provincia '{name.Trim()}' creada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProvince(string code, string name)
    {
        var province = await _context.EcuadorProvinces.FindAsync(code);
        if (province == null) return NotFound();
        province.Name = name.Trim();
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Provincia '{name.Trim()}' actualizada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> Profile()
    {
        var userId = CurrentUserId!;
        var profile = await _context.UserProfiles
            .AsNoTracking()
            .Include(p => p.Province)
            .Include(p => p.City)
            .FirstOrDefaultAsync(p => p.IdentityUserId == userId);

        var addresses = await _context.UserAddresses
            .AsNoTracking()
            .Include(a => a.Province)
            .Include(a => a.City)
            .Where(a => a.IdentityUserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAt)
            .ToListAsync();

        var provinces = await _context.EcuadorProvinces
            .AsNoTracking().OrderBy(p => p.Name).ToListAsync();

        var cities = await _context.EcuadorCities
            .AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        ViewBag.Profile = profile;
        ViewBag.Provinces = provinces;
        ViewBag.Cities = cities;
        return View(addresses);
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string firstName, string lastName, string cedula, string addressLine1, string addressLine2, string provinceCode, string cityCode, string? reference)
    {
        var userId = CurrentUserId!;
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.IdentityUserId == userId);
        if (profile == null) return RedirectToAction(nameof(Profile));

        profile.FirstName = firstName.Trim();
        profile.LastName = lastName.Trim();
        profile.Cedula = cedula.Trim();
        profile.AddressLine1 = addressLine1.Trim();
        profile.AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? "" : addressLine2.Trim();
        profile.ProvinceCode = provinceCode;
        profile.CityCode = cityCode;
        profile.Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        await _context.SaveChangesAsync();

        TempData["Success"] = "Perfil actualizado.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAddress(string label, string addressLine1, string addressLine2, string provinceCode, string cityCode, string? reference)
    {
        var userId = CurrentUserId!;
        var hasDefault = await _context.UserAddresses
            .AnyAsync(a => a.IdentityUserId == userId && a.IsDefault);

        _context.UserAddresses.Add(new UserAddress
        {
            IdentityUserId = userId,
            Label = string.IsNullOrWhiteSpace(label) ? "Casa" : label.Trim(),
            AddressLine1 = addressLine1.Trim(),
            AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? "" : addressLine2.Trim(),
            ProvinceCode = provinceCode,
            CityCode = cityCode,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            IsDefault = !hasDefault,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = "Dirección creada.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(long addressId)
    {
        var userId = CurrentUserId!;
        var addresses = await _context.UserAddresses
            .Where(a => a.IdentityUserId == userId).ToListAsync();

        foreach (var addr in addresses)
            addr.IsDefault = addr.UserAddressId == addressId;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Dirección predeterminada actualizada.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(long addressId)
    {
        var userId = CurrentUserId!;
        var address = await _context.UserAddresses
            .FirstOrDefaultAsync(a => a.UserAddressId == addressId && a.IdentityUserId == userId);

        if (address != null)
        {
            _context.UserAddresses.Remove(address);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Dirección eliminada.";
        }
        return RedirectToAction(nameof(Profile));
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
