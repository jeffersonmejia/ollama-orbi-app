using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;
using OrbiApp.Models.Delivery;
using OrbiApp.Models.Identity;
using OrbiApp.Models.Operations;
using OrbiApp.Models;
using Microsoft.Extensions.Options;
using OrbiApp.Application.Features.Delivery.Ports;
using OrbiApp.Application.Features.Payments.Ports;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Data;
using OrbiApp.Presentation.Services;

namespace OrbiApp.Controllers;

[Authorize]
public class DeliveryController : Controller
{
    private static readonly string[] ValidStatuses =
        { "Pendiente", "Comprado", "En preparación", "En camino", "Entregado", "Cancelado" };

    private readonly IApplicationDataSession _context;
    private readonly IPayPalGateway _payPalService;
    private readonly ISuggestMarketPriceUseCase _suggestMarketPrice;
    private readonly ApplicationDbContext _database;
    private readonly ILogger<DeliveryController> _logger;
    private readonly DeliveryPageCache _pageCache;

    public DeliveryController(
        IApplicationDataSession context,
        IPayPalGateway payPalService,
        ISuggestMarketPriceUseCase suggestMarketPrice,
        ApplicationDbContext database,
        ILogger<DeliveryController> logger,
        DeliveryPageCache pageCache)
    {
        _context = context;
        _payPalService = payPalService;
        _suggestMarketPrice = suggestMarketPrice;
        _database = database;
        _logger = logger;
        _pageCache = pageCache;
    }

    [NonAction]
    public async Task<IActionResult> Index(string? buscar, decimal? precioMinimo, decimal? precioMaximo, string? categoria, int page = 1, string? cityCode = null)
    {
        var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var totalTimer = Stopwatch.StartNew();
        var stageTimer = Stopwatch.StartNew();
        void LogStage(string stage)
        {
            _logger.LogInformation(
                "Delivery.Index timing {TraceId} AJAX={IsAjax}: {Stage} took {ElapsedMs} ms",
                HttpContext.TraceIdentifier,
                isAjaxRequest,
                stage,
                stageTimer.ElapsedMilliseconds);
            stageTimer.Restart();
        }

        var productsQuery = _context.Set<DeliveryProduct>()
            .AsNoTracking()
            .Include(product => product.Store).ThenInclude(store => store!.Province)
            .Include(product => product.Store).ThenInclude(store => store!.City)
            .Where(product => product.IsAvailable && product.Store.IsActive);

        string? userProvinceCode = null;
        string? userProvinceName = null;
        string? userCityName = null;
        string? userCityCode = null;
        string? userFirstName = null;
        var hasClientCityCode = Request.Query.ContainsKey("cityCode");
        if (isAjaxRequest && hasClientCityCode)
        {
            userCityCode = string.IsNullOrWhiteSpace(cityCode) ? null : cityCode;
            if (userCityCode is not null)
                productsQuery = productsQuery.Where(product => product.Store.CityCode == userCityCode);
        }
        else if (CurrentUserId is string userId)
        {
            var profile = await _pageCache.GetUserProfileAsync(_context, userId, HttpContext.RequestAborted);
            if (profile is not null)
            {
                userFirstName = profile.FirstName;
                userProvinceCode = profile.ProvinceCode;
                userProvinceName = profile.Province?.Name;
                userCityName = profile.City?.Name;
                userCityCode = profile.CityCode;
                productsQuery = productsQuery.Where(p => p.Store.CityCode == userCityCode);
            }
            else if (await _pageCache.GetPrimaryAddressAsync(_context, userId, HttpContext.RequestAborted) is { } primaryAddress)
            {
                userProvinceCode = primaryAddress.ProvinceCode;
                userProvinceName = primaryAddress.Province?.Name;
                userCityName = primaryAddress.City?.Name;
                userCityCode = primaryAddress.CityCode;
                productsQuery = productsQuery.Where(product => product.Store.CityCode == userCityCode);
            }
        }
        LogStage("profile/address lookup");

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
        LogStage("catalog count and page query");
        ViewBag.Buscar = buscar;
        ViewBag.PrecioMinimo = precioMinimo;
        ViewBag.PrecioMaximo = precioMaximo;
        ViewBag.Categoria = categoria;
        var addresses = new List<UserAddress>();
        var provinces = new List<EcuadorProvince>();
        var cities = new List<EcuadorCity>();
        if (User.IsInRole("Usuario") && CurrentUserId is string userId2)
        {
            if (!isAjaxRequest)
            {
                addresses = await _context.Set<UserAddress>().AsNoTracking()
                    .Where(address => address.IdentityUserId == userId2)
                    .Include(address => address.Province)
                    .Include(address => address.City)
                    .OrderByDescending(address => address.IsDefault)
                    .ThenBy(address => address.Label)
                    .ToListAsync();
                LogStage("address list");
                if (addresses.Count == 0)
                {
                    await EnsurePrimaryAddressAsync(userId2, addressAlreadyChecked: true);
                    LogStage("legacy profile address migration");
                    addresses = await _context.Set<UserAddress>().AsNoTracking()
                        .Where(address => address.IdentityUserId == userId2)
                        .Include(address => address.Province)
                        .Include(address => address.City)
                        .OrderByDescending(address => address.IsDefault)
                        .ThenBy(address => address.Label)
                        .ToListAsync();
                    LogStage("address list after legacy migration");
                }
                provinces = (await _pageCache.GetProvincesAsync(_context, HttpContext.RequestAborted)).ToList();
                LogStage("province list");
                cities = (await _pageCache.GetCitiesAsync(_context, HttpContext.RequestAborted)).ToList();
                LogStage("city list");
            }
        }

        var cartQuantities = new Dictionary<int, int>();
        if (User.IsInRole("Usuario") && User.Identity?.Name is string userEmail)
        {
            var productIds = products.Select(product => product.DeliveryProductId).ToArray();
            cartQuantities = await _context.Set<DeliveryCartItem>()
                .AsNoTracking()
                .Where(item => item.UserEmail == userEmail && productIds.Contains(item.DeliveryProductId))
                .ToDictionaryAsync(item => item.DeliveryProductId, item => item.Quantity);
        }
        LogStage("cart quantities");

        if (!isAjaxRequest)
        {
            ViewBag.UserProvinceName = userProvinceName;
            ViewBag.UserProvinceCode = userProvinceCode;
            ViewBag.UserCityCode = userCityCode;
            ViewBag.UserCityName = userCityName;
            ViewBag.UserFirstName = userFirstName;
        }

        var viewModel = new DeliveryCatalogViewModel
        {
            Products = products,
            Addresses = addresses,
            Provinces = provinces,
            Cities = cities,
            CartQuantities = cartQuantities
        };

        _logger.LogInformation(
            "Delivery.Index controller timing {TraceId} AJAX={IsAjax}: total {ElapsedMs} ms",
            HttpContext.TraceIdentifier,
            isAjaxRequest,
            totalTimer.ElapsedMilliseconds);

        if (isAjaxRequest)
            return PartialView("_ProductResults", viewModel);

        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Roles = "Usuario")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SessionContext()
    {
        if (CurrentUserId is not string userId)
            return Unauthorized(new { code = "authentication_required" });

        var profile = await _pageCache.GetUserProfileAsync(_context, userId, HttpContext.RequestAborted);
        var primaryAddress = await _pageCache.GetPrimaryAddressAsync(_context, userId, HttpContext.RequestAborted);

        return Json(new
        {
            displayName = profile?.FirstName,
            provinceCode = profile?.ProvinceCode ?? primaryAddress?.ProvinceCode,
            cityCode = profile?.CityCode ?? primaryAddress?.CityCode,
            hasAddress = primaryAddress is not null
        });
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCatalogAddress([FromForm] CatalogAddressInput input)
    {
        if (CurrentUserId is not string userId)
            return Unauthorized(new { code = "authentication_required", message = "Inicia sesión para guardar tu dirección." });

        if (!ModelState.IsValid)
            return BadRequest(new
            {
                code = "validation_error",
                errors = ModelState.Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(entry => entry.Key, entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray())
            });

        var label = input.Label.Trim();
        UserAddress? address = null;
        if (input.UserAddressId.HasValue)
        {
            address = await _context.Set<UserAddress>().SingleOrDefaultAsync(item =>
                item.UserAddressId == input.UserAddressId.Value && item.IdentityUserId == userId);
            if (address is null)
                return NotFound(new { code = "address_not_found", message = "No encontramos esa dirección." });
        }

        var duplicateLabel = await _context.Set<UserAddress>().AnyAsync(existingAddress =>
            existingAddress.IdentityUserId == userId && existingAddress.Label.ToLower() == label.ToLower() &&
            existingAddress.UserAddressId != (input.UserAddressId ?? 0));
        if (duplicateLabel)
            return Conflict(new { code = "validation_error", errors = new { Label = new[] { "Ya tienes una dirección con ese nombre." } } });

        var city = await _context.Set<EcuadorCity>().AsNoTracking()
            .Where(item => item.Code == input.CityCode && item.ProvinceCode == input.ProvinceCode)
            .Select(item => new { item.Code, item.Name, item.ProvinceCode, ProvinceName = item.Province.Name })
            .FirstOrDefaultAsync();
        if (city is null)
            return BadRequest(new { code = "validation_error", errors = new { CityCode = new[] { "La ciudad no pertenece a la provincia seleccionada." } } });

        try
        {
            await using var transaction = await _database.Database.BeginTransactionAsync();
            address ??= new UserAddress
            {
                IdentityUserId = userId,
            };

            address.Label = label;
            address.AddressLine1 = input.AddressLine1.Trim();
            address.AddressLine2 = input.AddressLine2.Trim();
            address.ProvinceCode = city.ProvinceCode;
            address.CityCode = city.Code;
            address.Reference = input.Reference.Trim();
            address.IsDefault = true;
            address.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.Set<UserAddress>()
                .Where(item => item.IdentityUserId == userId && item.IsDefault && item.UserAddressId != address.UserAddressId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false));
            if (!input.UserAddressId.HasValue)
                _context.Set<UserAddress>().Add(address);

            var profile = await _context.Set<UserProfile>().SingleOrDefaultAsync(item => item.IdentityUserId == userId);
            if (profile is not null)
            {
                profile.AddressLine1 = address.AddressLine1;
                profile.AddressLine2 = address.AddressLine2;
                profile.ProvinceCode = address.ProvinceCode;
                profile.CityCode = address.CityCode;
                profile.Reference = address.Reference;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            _pageCache.InvalidateUser(userId);
            return Json(new
            {
                code = "address_saved",
                address = new
                {
                    address.UserAddressId,
                    address.Label,
                    address.AddressLine1,
                    address.AddressLine2,
                    address.Reference,
                    address.ProvinceCode,
                    provinceName = city.ProvinceName,
                    address.CityCode,
                    cityName = city.Name
                }
            });
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "No se pudo guardar la dirección del usuario {UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                code = "address_save_failed",
                message = "No se pudo guardar la dirección. Inténtalo de nuevo."
            });
        }
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

        var product = await _context.Set<DeliveryProduct>()
            .Include(p => p.Store)
            .FirstOrDefaultAsync(p => p.DeliveryProductId == productId);

        if (product == null || !product.IsAvailable || !product.Store.IsActive)
        {
            TempData["Error"] = "Producto no disponible.";
            return RedirectToAction(nameof(Index));
        }

        var email = User.Identity!.Name!;
        var existing = await _context.Set<DeliveryCartItem>()
            .FirstOrDefaultAsync(c => c.UserEmail == email && c.DeliveryProductId == productId);

        if (existing != null)
        {
            existing.Quantity = Math.Min(999, existing.Quantity + quantity);
        }
        else
        {
            _context.Set<DeliveryCartItem>().Add(new DeliveryCartItem
            {
                UserEmail = email,
                DeliveryProductId = productId,
                Quantity = quantity,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = $"{product.Name} agregado al carrito.";
        return Redirect("/Delivery/Cart");
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeCatalogCartQuantity(int productId, int delta, string? returnUrl)
    {
        var isAjaxRequest = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var userId = CurrentUserId;
        var userEmail = User.Identity?.Name;
        if (delta is not (-1 or 1) || userId is null || userEmail is null)
        {
            return BadRequest(new { code = "invalid_request" });
        }

        var product = await _context.Set<DeliveryProduct>()
            .Include(item => item.Store)
            .FirstOrDefaultAsync(item => item.DeliveryProductId == productId);
        if (product is null || !product.IsAvailable || !product.Store.IsActive)
        {
            return NotFound(new { code = "product_unavailable" });
        }

        if (delta > 0)
        {
            await EnsurePrimaryAddressAsync(userId);
            var hasAddress = await _pageCache.GetPrimaryAddressAsync(_context, userId, HttpContext.RequestAborted) is not null;
            if (!hasAddress)
            {
                if (isAjaxRequest)
                {
                    return Conflict(new
                    {
                        code = "address_required",
                        message = "Para agregar productos al carrito, primero registra una dirección de entrega."
                    });
                }

                var addressUrl = Url.Page("/Account/Manage/Addresses", new { area = "Identity" });
                return !string.IsNullOrWhiteSpace(addressUrl)
                    ? LocalRedirect(addressUrl)
                    : RedirectToAction(nameof(Index));
            }
        }

        var cartItem = await _context.Set<DeliveryCartItem>()
            .FirstOrDefaultAsync(item => item.UserEmail == userEmail && item.DeliveryProductId == productId);
        var quantity = cartItem?.Quantity ?? 0;

        if (delta > 0)
        {
            if (quantity >= Math.Min(product.Stock, 999))
            {
                if (isAjaxRequest)
                {
                    return Conflict(new { code = "stock_limit", message = "No hay más unidades disponibles." });
                }

                TempData["Error"] = "No hay más unidades disponibles.";
                return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? LocalRedirect(returnUrl)
                    : RedirectToAction(nameof(Index));
            }

            if (cartItem is null)
            {
                _context.Set<DeliveryCartItem>().Add(new DeliveryCartItem
                {
                    UserEmail = userEmail,
                    DeliveryProductId = productId,
                    Quantity = 1,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                cartItem.Quantity++;
            }

            quantity++;
        }
        else if (cartItem is not null)
        {
            if (cartItem.Quantity <= 1)
            {
                _context.Set<DeliveryCartItem>().Remove(cartItem);
            }
            else
            {
                cartItem.Quantity--;
            }

            quantity--;
        }

        await _context.SaveChangesAsync();
        if (isAjaxRequest)
        {
            return Json(new { quantity, stock = product.Stock });
        }

        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }

    [HttpPost("/Delivery/Checkout")]
    [HttpPost("/api/delivery/checkout")]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout()
    {
        var isApiRequest = Request.Path.StartsWithSegments("/api/delivery/checkout");
        IActionResult CheckoutFailure(int statusCode, string code, string message)
        {
            if (isApiRequest)
                return StatusCode(statusCode, new { code, message });

            TempData["Error"] = message;
            return Redirect("/Delivery/Cart");
        }

        var email = User.Identity!.Name!;
        var userId = CurrentUserId;

        var items = await _context.Set<DeliveryCartItem>()
            .Include(c => c.Product).ThenInclude(p => p.Store)
            .Where(c => c.UserEmail == email)
            .ToListAsync();

        if (!items.Any())
        {
            return CheckoutFailure(StatusCodes.Status400BadRequest, "cart_empty", "El carrito está vacío.");
        }

        var hasAddress = userId is not null &&
            await _pageCache.GetPrimaryAddressAsync(_context, userId, HttpContext.RequestAborted) is not null;
        if (!hasAddress)
        {
            return CheckoutFailure(StatusCodes.Status409Conflict, "address_required", "Agrega una dirección de entrega antes de pagar.");
        }

        var deliveryAddress = await _context.Set<UserAddress>()
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
            _context.Set<DeliveryOrder>().Add(order);
            await _context.SaveChangesAsync();

            _context.Set<OrderStatusHistory>().Add(new OrderStatusHistory
            {
                DeliveryOrderId = order.DeliveryOrderId,
                ChangedByUserId = userId,
                NewStatus = "Pendiente",
                Note = "Pedido creado desde carrito — pago: PayPal"
            });

            _context.Set<DeliveryPayment>().Add(new DeliveryPayment
            {
                DeliveryOrderId = order.DeliveryOrderId,
                ExternalId = $"PENDING-{Guid.NewGuid():N}",
                Provider = "PayPal",
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
            _context.Set<DeliveryCartItem>().RemoveRange(items);
            await _context.SaveChangesAsync();
            return CheckoutFailure(StatusCodes.Status400BadRequest, "minimum_total", "El total mínimo para pagar es $1.00.");
        }

        var firstOrderId = createdOrderIds.First();
        var firstPayment = await _context.Set<DeliveryPayment>()
            .FirstOrDefaultAsync(p => p.DeliveryOrderId == firstOrderId);

        string paymentUrl;

        string reference = $"Dash Order #{string.Join(",", createdOrderIds)}";
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
            _logger.LogWarning(ex, "PayPal order creation failed for the delivery checkout.");
            return CheckoutFailure(StatusCodes.Status503ServiceUnavailable, "payment_unavailable", "No se pudo iniciar el pago. Inténtalo de nuevo más tarde.");
        }

        _context.Set<DeliveryCartItem>().RemoveRange(items);
        await _context.SaveChangesAsync();

        return isApiRequest ? Ok(new { redirectUrl = paymentUrl }) : Redirect(paymentUrl);
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> PaymentSuccess(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "PayPal no devolvió un token válido.";
            return Redirect("/Delivery/MyOrders");
        }

        await using var transaction = await _database.Database.BeginTransactionAsync();
        var paymentId = await _context.Set<DeliveryPayment>()
            .Where(p => p.Provider == "PayPal" && p.ExternalId == token)
            .Select(p => (long?)p.PaymentId)
            .FirstOrDefaultAsync();

        if (paymentId == null)
        {
            TempData["Error"] = "Pago no encontrado o ya procesado.";
            return Redirect("/Delivery/MyOrders");
        }

        await _database.AcquirePaymentLockAsync("delivery-payment", paymentId.Value);

        var payment = await _context.Set<DeliveryPayment>()
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Provider == "PayPal" && p.ExternalId == token);

        if (payment == null) return NotFound();

        if (payment.Status == "Aprobado")
        {
            await transaction.CommitAsync();
            TempData["Success"] = "El pago ya fue procesado.";
            return Redirect("/Delivery/MyOrders");
        }

        try
        {
            var capture = await _payPalService.CaptureOrderAsync(token);

            payment.Status = capture.Status == "COMPLETED" ? "Aprobado" : capture.Status;
            payment.ExternalId = capture.CaptureId;
            payment.ConfirmedAt = DateTimeOffset.UtcNow;

            if (capture.Status == "COMPLETED")
            {
                var orders = await _context.Set<DeliveryOrder>()
                    .Where(o => payment.DeliveryOrderId == o.DeliveryOrderId)
                    .ToListAsync();
                foreach (var o in orders)
                    o.Status = "Comprado";
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = "¡Pago con PayPal completado exitosamente!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPal payment confirmation failed.");
            TempData["Error"] = "No se pudo confirmar el pago. Inténtalo de nuevo o contacta a soporte.";
        }

        return Redirect("/Delivery/MyOrders");
    }

    [Authorize(Roles = "Usuario")]
    public IActionResult PaymentCancel()
    {
        TempData["Error"] = "El pago con PayPal fue cancelado.";
        return Redirect("/Delivery/MyOrders");
    }

    [Authorize(Roles = "Vendedor")]
    public async Task<IActionResult> SellerStore()
    {
        var userId = CurrentUserId;
        var store = await _context.Set<DeliveryStore>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

        var profile = await _context.Set<UserProfile>().AsNoTracking()
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

        var profile = await _context.Set<UserProfile>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdentityUserId == userId);
        if (profile == null)
        {
            TempData["Error"] = "No se encontró tu perfil de usuario.";
            return RedirectToAction(nameof(SellerStore));
        }

        var store = await _context.Set<DeliveryStore>().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
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
            _context.Set<DeliveryStore>().Add(store);
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
        var myStore = await _context.Set<DeliveryStore>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.OwnerUserId == userId);

        if (myStore == null)
        {
            TempData["Error"] = "Primero debes registrar tu tienda.";
            return RedirectToAction(nameof(SellerStore));
        }

        var query = _context.Set<DeliveryProduct>()
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
    public async Task<IActionResult> CreateProduct(string name, decimal price, decimal unitCost, int stock, string? imageUrl)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(name) || price < 0 || unitCost < 0 || stock < 0 || stock > 999 || !TryNormalizeProductImageUrl(imageUrl, out imageUrl))
        {
            TempData["Error"] = "Completa los campos correctamente. El stock debe ser 0-999 y la imagen debe usar una URL HTTPS válida.";
            return RedirectToAction(nameof(SellerInventory));
        }

        var store = await _context.Set<DeliveryStore>().FirstOrDefaultAsync(s => s.OwnerUserId == userId);
        if (store == null)
        {
            TempData["Error"] = "Primero debes registrar tu tienda.";
            return RedirectToAction(nameof(SellerStore));
        }

        _context.Set<DeliveryProduct>().Add(new DeliveryProduct
        {
            DeliveryStoreId = store.DeliveryStoreId,
            CreatedByUserId = userId,
            Name = name.Trim(),
            ImageUrl = imageUrl,
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
    public async Task<IActionResult> UpdateProduct(int productId, string name, decimal price, decimal unitCost, int stock, string? imageUrl)
    {
        var userId = CurrentUserId;
        var product = await _context.Set<DeliveryProduct>().FirstOrDefaultAsync(p =>
            p.DeliveryProductId == productId && p.CreatedByUserId == userId);

        if (product == null)
        {
            TempData["Error"] = "Producto no encontrado o no tienes permiso para editarlo.";
            return RedirectToAction(nameof(SellerInventory));
        }

        if (string.IsNullOrWhiteSpace(name) || price < 0 || unitCost < 0 || stock < 0 || stock > 999 || !TryNormalizeProductImageUrl(imageUrl, out imageUrl))
        {
            TempData["Error"] = "Completa los campos correctamente. La imagen debe usar una URL HTTPS válida.";
            return RedirectToAction(nameof(SellerInventory));
        }

        product.Name = name.Trim();
        product.ImageUrl = imageUrl;
        product.Price = price;
        product.UnitCost = unitCost;
        product.Stock = stock;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Producto '{name.Trim()}' actualizado.";
        return RedirectToAction(nameof(SellerInventory));
    }

    private static bool TryNormalizeProductImageUrl(string? value, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ||
            (normalized.Length <= 500 && Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
    }

    [HttpPost]
    [Authorize(Roles = "Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuggestPrice(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return Json(new { ok = false, message = "Escribe el nombre del producto." });

        var userId = CurrentUserId;
        var profile = await _context.Set<UserProfile>().AsNoTracking()
            .Include(p => p.Province).Include(p => p.City)
            .FirstOrDefaultAsync(p => p.IdentityUserId == userId);

        var provinceName = profile?.Province?.Name ?? "Guayaquil";
        var cityName = profile?.City?.Name ?? "Guayaquil";

        try
        {
            var analysis = await _suggestMarketPrice.ExecuteAsync(productName.Trim(), HttpContext.RequestAborted);
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
            _logger.LogWarning(ex, "Delivery price suggestion request failed.");
            return Json(new { ok = false, message = "No se pudo obtener una sugerencia de precio en este momento." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int productId)
    {
        var userId = CurrentUserId;
        var product = await _context.Set<DeliveryProduct>().FirstOrDefaultAsync(p =>
            p.DeliveryProductId == productId && p.CreatedByUserId == userId);

        if (product == null)
        {
            TempData["Error"] = "Producto no encontrado o no tienes permiso para eliminarlo.";
            return RedirectToAction(nameof(SellerInventory));
        }

        _context.Set<DeliveryProduct>().Remove(product);
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
        var query = _context.Set<DeliveryStore>().AsNoTracking()
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
        ViewBag.Provincias = (await _pageCache.GetProvincesAsync(_context, HttpContext.RequestAborted)).ToList();
        ViewBag.Ciudades = (await _pageCache.GetCitiesAsync(_context, HttpContext.RequestAborted))
            .Where(city => string.IsNullOrWhiteSpace(provincia) || city.ProvinceCode == provincia)
            .ToList();

        var stores = await PaginatedList<DeliveryStore>.CreateAsync(
            query.OrderBy(s => s.Name), Math.Max(1, page), 5);
        var ownerIds = stores
            .Where(store => !string.IsNullOrWhiteSpace(store.OwnerUserId))
            .Select(store => store.OwnerUserId!)
            .Distinct()
            .ToList();
        var ownerProfiles = await _context.Set<UserProfile>().AsNoTracking()
            .Where(profile => ownerIds.Contains(profile.IdentityUserId))
            .ToDictionaryAsync(profile => profile.IdentityUserId);
        var ownerEmails = await _context.Set<Microsoft.AspNetCore.Identity.IdentityUser>().AsNoTracking()
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
        var query = _context.Set<Microsoft.AspNetCore.Identity.IdentityUser>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim().ToLower();
            var profileUserIds = await _context.Set<UserProfile>()
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
        var user = await _context.Set<Microsoft.AspNetCore.Identity.IdentityUser>().FindAsync(userId);
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

        var order = await _context.Set<DeliveryOrder>().FindAsync(orderId);
        if (order == null) return NotFound();

        if (User.IsInRole("Repartidor"))
        {
            if (order.DeliveryPersonEmail != User.Identity!.Name ||
                (status != "Comprado" && status != "En camino" && status != "Entregado"))
                return Forbid();
        }

        var previousStatus = order.Status;
        if (previousStatus == status)
            return Redirect(User.IsInRole("Administrador") ? Url.Action(nameof(AdminOrders))! : "/Delivery/Deliveries");

        order.Status = status;
        _context.Set<OrderStatusHistory>().Add(new OrderStatusHistory
        {
            DeliveryOrderId = order.DeliveryOrderId,
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            PreviousStatus = previousStatus,
            NewStatus = status
        });
        await _context.SaveChangesAsync();
        return Redirect(User.IsInRole("Administrador") ? Url.Action(nameof(AdminOrders))! : "/Delivery/Deliveries");
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

        var store = await _context.Set<DeliveryStore>().FindAsync(storeId);
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
        var store = await _context.Set<DeliveryStore>().FindAsync(storeId);
        if (store == null) return NotFound();

        store.IsActive = !store.IsActive;
        await _context.SaveChangesAsync();
        TempData["Success"] = $"{store.Name} ahora está {(store.IsActive ? "activa" : "inactiva")}.";
        return RedirectToAction(nameof(AdminStores));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminAuditLog(string? buscar, int page = 1)
    {
        var query = _context.Set<AuditLog>().AsNoTracking().Include(a => a.User).AsQueryable();
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
            from userRole in _context.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().AsNoTracking()
            join role in _context.Set<Microsoft.AspNetCore.Identity.IdentityRole>().AsNoTracking() on userRole.RoleId equals role.Id
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
        var query = _context.Set<InventoryMovement>().AsNoTracking()
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
        var query = _context.Set<DeliveryPayment>().AsNoTracking()
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

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AdminLocations(string? buscar, int page = 1)
    {
        var query = _context.Set<EcuadorCity>().AsNoTracking()
            .Include(c => c.Province)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, term) || EF.Functions.ILike(c.Province.Name, term));
        }
        ViewBag.Buscar = buscar;
        ViewBag.Provincias = await _context.Set<EcuadorProvince>().OrderBy(p => p.Name).ToListAsync();
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
        if (await _context.Set<EcuadorCity>().AnyAsync(c => c.Code == code.Trim()))
        {
            TempData["Error"] = $"Ya existe una ciudad con código {code.Trim()}.";
            return RedirectToAction(nameof(AdminLocations));
        }
        _context.Set<EcuadorCity>().Add(new EcuadorCity { Code = code.Trim(), ProvinceCode = provinceCode.Trim(), Name = name.Trim() });
        await _context.SaveChangesAsync();
        _pageCache.InvalidateLocations();
        TempData["Success"] = $"Ciudad '{name.Trim()}' creada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCity(string code, string name)
    {
        var city = await _context.Set<EcuadorCity>().FindAsync(code);
        if (city == null) return NotFound();
        city.Name = name.Trim();
        await _context.SaveChangesAsync();
        _pageCache.InvalidateLocations();
        TempData["Success"] = $"Ciudad '{name.Trim()}' actualizada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCity(string code)
    {
        var city = await _context.Set<EcuadorCity>().FindAsync(code);
        if (city == null) return NotFound();
        _context.Set<EcuadorCity>().Remove(city);
        await _context.SaveChangesAsync();
        _pageCache.InvalidateLocations();
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
        if (await _context.Set<EcuadorProvince>().AnyAsync(p => p.Code == code.Trim()))
        {
            TempData["Error"] = $"Ya existe una provincia con código {code.Trim()}.";
            return RedirectToAction(nameof(AdminLocations));
        }
        _context.Set<EcuadorProvince>().Add(new EcuadorProvince { Code = code.Trim(), Name = name.Trim() });
        await _context.SaveChangesAsync();
        _pageCache.InvalidateLocations();
        TempData["Success"] = $"Provincia '{name.Trim()}' creada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProvince(string code, string name)
    {
        var province = await _context.Set<EcuadorProvince>().FindAsync(code);
        if (province == null) return NotFound();
        province.Name = name.Trim();
        await _context.SaveChangesAsync();
        _pageCache.InvalidateLocations();
        TempData["Success"] = $"Provincia '{name.Trim()}' actualizada.";
        return RedirectToAction(nameof(AdminLocations));
    }

    [Authorize(Roles = "Usuario")]
    public async Task<IActionResult> Profile()
    {
        var userId = CurrentUserId!;
        var profile = await _pageCache.GetUserProfileAsync(_context, userId, HttpContext.RequestAborted);

        var addresses = await _context.Set<UserAddress>()
            .AsNoTracking()
            .Include(a => a.Province)
            .Include(a => a.City)
            .Where(a => a.IdentityUserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.UpdatedAt)
            .ToListAsync();

        var provinces = (await _pageCache.GetProvincesAsync(_context, HttpContext.RequestAborted)).ToList();
        var cities = (await _pageCache.GetCitiesAsync(_context, HttpContext.RequestAborted)).ToList();

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
        var profile = await _context.Set<UserProfile>()
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
        _pageCache.InvalidateUser(userId);

        TempData["Success"] = "Perfil actualizado.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAddress(string label, string addressLine1, string addressLine2, string provinceCode, string cityCode, string? reference)
    {
        var userId = CurrentUserId!;
        var hasDefault = await _context.Set<UserAddress>()
            .AnyAsync(a => a.IdentityUserId == userId && a.IsDefault);

        _context.Set<UserAddress>().Add(new UserAddress
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
        _pageCache.InvalidateUser(userId);

        TempData["Success"] = "Dirección creada.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultAddress(long addressId)
    {
        var userId = CurrentUserId!;
        var addresses = await _context.Set<UserAddress>()
            .Where(a => a.IdentityUserId == userId).ToListAsync();

        foreach (var addr in addresses)
            addr.IsDefault = addr.UserAddressId == addressId;

        await _context.SaveChangesAsync();
        _pageCache.InvalidateUser(userId);
        TempData["Success"] = "Dirección predeterminada actualizada.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Authorize(Roles = "Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAddress(long addressId)
    {
        var userId = CurrentUserId!;
        var address = await _context.Set<UserAddress>()
            .FirstOrDefaultAsync(a => a.UserAddressId == addressId && a.IdentityUserId == userId);

        if (address != null)
        {
            _context.Set<UserAddress>().Remove(address);
            await _context.SaveChangesAsync();
            _pageCache.InvalidateUser(userId);
            TempData["Success"] = "Dirección eliminada.";
        }
        return RedirectToAction(nameof(Profile));
    }

    private IQueryable<DeliveryOrder> OrderQuery() => _context.Set<DeliveryOrder>()
        .AsNoTracking()
        .Include(order => order.Store)
        .Include(order => order.Items)
        .OrderByDescending(order => order.CreatedAt);

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task EnsurePrimaryAddressAsync(string userId, bool addressAlreadyChecked = false)
    {
        if (!addressAlreadyChecked && await _context.Set<UserAddress>().AnyAsync(address => address.IdentityUserId == userId)) return;

        var profile = await _context.Set<UserProfile>().AsNoTracking().SingleOrDefaultAsync(item => item.IdentityUserId == userId);
        if (profile is null || string.IsNullOrWhiteSpace(profile.AddressLine1)) return;

        _context.Set<UserAddress>().Add(new UserAddress
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
        _pageCache.InvalidateUser(userId);
    }
}
