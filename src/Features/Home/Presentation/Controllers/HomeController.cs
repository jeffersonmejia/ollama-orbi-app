using OrbiApp.Application.Features.Assistant.Ports;
using OrbiApp.Application.Features.Operations.Ports;
using OrbiApp.Application.Features.Shared.Ports;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Models;
using OrbiApp.Models.Identity;
using OrbiApp.Models.Delivery;
using OrbiApp.Models.Operations;

namespace OrbiApp.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IApplicationDataSession _identityContext;
    private readonly IProductAssistant _ollama;
    private readonly IBackupManager _backups;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IApplicationDataSession identityContext,
        IProductAssistant ollama,
        IBackupManager backups,
        ILogger<HomeController> logger)
    {
        _identityContext = identityContext;
        _ollama = ollama;
        _backups = backups;
        _logger = logger;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Usuario"))
        {
            return RedirectToAction("Index", "Delivery");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["FirstName"] = await _identityContext.Set<UserProfile>()
                .AsNoTracking()
                .Where(profile => profile.IdentityUserId == userId)
                .Select(profile => profile.FirstName)
                .FirstOrDefaultAsync();

            if (User.IsInRole("Administrador"))
            {
                var usersPerMonth = await _identityContext.Set<UserProfile>()
                    .AsNoTracking()
                    .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .ToListAsync();

                var ordersPerMonth = await _identityContext.Set<DeliveryOrder>()
                    .AsNoTracking()
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.Status })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Status, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .ToListAsync();

                var storesPerMonth = await _identityContext.Set<DeliveryStore>()
                    .AsNoTracking()
                    .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .ToListAsync();

                ViewBag.UsersPerMonth = usersPerMonth;
                ViewBag.OrdersPerMonth = ordersPerMonth;
                ViewBag.StoresPerMonth = storesPerMonth;
                ViewBag.TotalUsers = usersPerMonth.Sum(x => x.Count);
                ViewBag.TotalOrders = ordersPerMonth.Sum(x => x.Count);
                ViewBag.TotalStores = storesPerMonth.Sum(x => x.Count);
            }
        }

        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Assistant()
    {
        return Redirect("/app/asistente");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ChartStats(string range = "6h")
    {
        var since = range switch
        {
            "12h" => DateTime.UtcNow.AddHours(-12),
            "1d" => DateTime.UtcNow.AddDays(-1),
            "month" => new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => DateTime.UtcNow.AddHours(-6)
        };

        bool isMonth = range == "month";
        var email = User.Identity?.Name ?? "";
        Func<int, int, string> fmt = isMonth
            ? (d, m) => $"{d:D2}/{m:D2}"
            : (d, m) => $"{d:D2}:00";

        if (User.IsInRole("Administrador"))
        {
            var users = isMonth
                ? (await _identityContext.Set<UserProfile>().AsNoTracking()
                    .Where(p => p.CreatedAt >= since)
                    .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month, p.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.Set<UserProfile>().AsNoTracking()
                    .Where(p => p.CreatedAt >= since)
                    .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month, p.CreatedAt.Day, p.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            var stores = isMonth
                ? (await _identityContext.Set<DeliveryStore>().AsNoTracking()
                    .Where(s => s.CreatedAt >= since)
                    .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month, s.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.Set<DeliveryStore>().AsNoTracking()
                    .Where(s => s.CreatedAt >= since)
                    .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month, s.CreatedAt.Day, s.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            return Json(new
            {
                Chart1Title = "Usuarios registrados",
                Chart1Color = "#D2006E",
                Chart1 = users.Select(u => new { Label = fmt(u.Hour, u.Month), Count = u.Count }),
                Chart1Total = users.Sum(u => u.Count),
                Chart2Title = "Tiendas registradas",
                Chart2Color = "#7A2852",
                Chart2 = stores.Select(s => new { Label = fmt(s.Hour, s.Month), Count = s.Count }),
                Chart2Total = stores.Sum(s => s.Count)
            });
        }

        if (User.IsInRole("Vendedor"))
        {
            var pedidos = isMonth
                ? (await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            var productos = isMonth
                ? (await _identityContext.Set<DeliveryProduct>().AsNoTracking().Include(p => p.Store)
                    .Where(p => p.Store.CreatedAt <= since)
                    .GroupBy(p => new { p.Store.CreatedAt.Year, p.Store.CreatedAt.Month, p.Store.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.Set<DeliveryProduct>().AsNoTracking().Include(p => p.Store)
                    .Where(p => p.Store.CreatedAt <= since)
                    .GroupBy(p => new { p.Store.CreatedAt.Year, p.Store.CreatedAt.Month, p.Store.CreatedAt.Day, p.Store.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            return Json(new
            {
                Chart1Title = "Pedidos recibidos",
                Chart1Color = "#7A2852",
                Chart1 = pedidos.Select(o => new { Label = fmt(o.Hour, o.Month), Count = o.Count }),
                Chart1Total = pedidos.Sum(o => o.Count),
                Chart2Title = "Productos en catálogo",
                Chart2Color = "#146c2e",
                Chart2 = productos.Select(p => new { Label = fmt(p.Hour, p.Month), Count = p.Count }),
                Chart2Total = productos.Sum(p => p.Count)
            });
        }

        if (User.IsInRole("Repartidor"))
        {
            var asignados = isMonth
                ? (await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.DeliveryPersonEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.DeliveryPersonEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            var completadas = isMonth
                ? (await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.DeliveryPersonEmail == email && o.Status == "Entregado" && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.DeliveryPersonEmail == email && o.Status == "Entregado" && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            return Json(new
            {
                Chart1Title = "Mis entregas",
                Chart1Color = "#146c2e",
                Chart1 = asignados.Select(o => new { Label = fmt(o.Hour, o.Month), Count = o.Count }),
                Chart1Total = asignados.Sum(o => o.Count),
                Chart2Title = "Completadas",
                Chart2Color = "#D2006E",
                Chart2 = completadas.Select(o => new { Label = fmt(o.Hour, o.Month), Count = o.Count }),
                Chart2Total = completadas.Sum(o => o.Count)
            });
        }

        if (User.IsInRole("Usuario"))
        {
            var misPedidos = isMonth
                ? (await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            var gasto = isMonth
                ? (await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Total = g.Sum(o => o.Total) })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                    .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Total = g.Sum(o => o.Total) })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            return Json(new
            {
                Chart1Title = "Mis pedidos",
                Chart1Color = "#D2006E",
                Chart1 = misPedidos.Select(o => new { Label = fmt(o.Hour, o.Month), Count = o.Count }),
                Chart1Total = misPedidos.Sum(o => o.Count),
                Chart2Title = "Gasto acumulado",
                Chart2Color = "#7A2852",
                Chart2 = gasto.Select(o => new { Label = fmt(o.Hour, o.Month), Count = (int)o.Total }),
                Chart2Total = (decimal)gasto.Sum(o => o.Total)
            });
        }

        return Json(new
        {
            Chart1Title = "Sin datos",
            Chart1Color = "#D2006E",
            Chart1 = Array.Empty<object>(),
            Chart1Total = 0,
            Chart2Title = "Sin datos",
            Chart2Color = "#7A2852",
            Chart2 = Array.Empty<object>(),
            Chart2Total = 0
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [RequestTimeout("assistant")]
    public async Task ProductChat(string message, string? context, CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await Response.WriteAsync("El asistente está temporalmente deshabilitado.", cancellationToken);
        return;

#pragma warning disable CS0162
        message = message?.Trim() ?? string.Empty;
        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        if (message.Length is < 2 or > 500)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteChatEventAsync("error", "Escribe una consulta de entre 2 y 500 caracteres.", cancellationToken);
            return;
        }

        try
        {
            await Response.StartAsync(cancellationToken);
            await WriteChatEventAsync("status", "Consultando datos...", cancellationToken);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var roleNames = User.Claims
                .Where(claim => claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var primaryRole = roleNames.FirstOrDefault(role => role == "Administrador")
                ?? roleNames.FirstOrDefault(role => role == "Vendedor")
                ?? roleNames.FirstOrDefault(role => role == "Repartidor")
                ?? roleNames.FirstOrDefault(role => role == "Usuario")
                ?? "Público";

            var account = userId is null
                ? null
                : await _identityContext.Set<Microsoft.AspNetCore.Identity.IdentityUser>().AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => new { user.UserName, user.Email })
                    .FirstOrDefaultAsync(cancellationToken);
            var profile = userId is null
                ? null
                : await _identityContext.Set<UserProfile>().AsNoTracking()
                    .Where(item => item.IdentityUserId == userId)
                    .Select(item => new { item.FirstName, item.LastName })
                    .FirstOrDefaultAsync(cancellationToken);
            var fullName = profile is null
                ? account?.UserName ?? "Visitante"
                : $"{profile.FirstName} {profile.LastName}".Trim();
            var email = account?.Email ?? User.Identity?.Name ?? string.Empty;
            var safePageContext = User.Identity?.IsAuthenticated == true
                ? primaryRole.ToLowerInvariant()
                : context is "login" or "register" or "logout" ? context : "public_home";
            var databaseContext = await BuildDatabaseContextAsync(
                message, primaryRole, userId, email, cancellationToken);
            var authoritativeAnswer = await BuildAuthoritativeAnswerAsync(
                message, primaryRole, userId, fullName, cancellationToken);
            var assistantContext = $"""
                IDENTIDAD_AUTENTICADA:
                Nombre: {fullName}
                Nombre de usuario: {account?.UserName ?? "visitante"}
                Correo: {(string.IsNullOrWhiteSpace(email) ? "no autenticado" : email)}
                Rol: {primaryRole}
                Roles: {(roleNames.Count == 0 ? "ninguno" : string.Join(", ", roleNames))}
                Permiso de consulta: {(primaryRole == "Administrador" ? "lectura global" : "lectura del catálogo y de sus propios registros")}

                CONTEXTO_DE_PAGINA:
                {GetAssistantContext(safePageContext)}

                DATOS_CONSULTADOS_EN_POSTGRESQL:
                {databaseContext}

                RESPUESTA_AUTORITATIVA_SI_APLICA:
                {authoritativeAnswer ?? "No aplica. Redacta la respuesta usando los datos consultados."}
                """;

            await WriteChatEventAsync("status", "Generando respuesta...", cancellationToken);
            var draft = new StringBuilder();
            await foreach (var chunk in _ollama.StreamChatAsync(
                message, assistantContext, cancellationToken))
            {
                draft.Append(chunk);
                await WriteChatEventAsync("delta", chunk, cancellationToken);
            }

            var reviewed = await _ollama.ReviewChatAsync(
                message, assistantContext, draft.ToString(), cancellationToken);
            await WriteChatEventAsync("final", authoritativeAnswer ?? reviewed, cancellationToken);
            await WriteChatEventAsync("done", string.Empty, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Assistant chat request failed.");
            Response.StatusCode = Response.HasStarted
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable;
            await WriteChatEventAsync(
                "error",
                exception is HttpRequestException or TaskCanceledException
                    ? "El asistente no está disponible. Verifica que Ollama esté encendido."
                    : "No pude completar la consulta en este momento.",
                cancellationToken);
        }
#pragma warning restore CS0162
    }

    private async Task<string> BuildDatabaseContextAsync(
        string message,
        string role,
        string? userId,
        string email,
        CancellationToken cancellationToken)
    {
        var facts = new StringBuilder();
        var activeStores = await _identityContext.Set<DeliveryStore>().AsNoTracking()
            .CountAsync(store => store.IsActive, cancellationToken);
        var availableProducts = await _identityContext.Set<DeliveryProduct>().AsNoTracking()
            .CountAsync(product => product.IsAvailable && product.Store.IsActive, cancellationToken);
        facts.AppendLine($"Catálogo: {activeStores:N0} tiendas activas, {availableProducts:N0} productos disponibles.");

        if (role == "Administrador")
        {
            facts.AppendLine($"Cuentas totales: {await _identityContext.Set<Microsoft.AspNetCore.Identity.IdentityUser>().CountAsync(cancellationToken):N0}.");
            facts.AppendLine($"Pedidos totales: {await _identityContext.Set<DeliveryOrder>().CountAsync(cancellationToken):N0}.");
            facts.AppendLine($"Pagos totales: {await _identityContext.Set<DeliveryPayment>().CountAsync(cancellationToken):N0}.");
            facts.AppendLine($"Incidencias totales: {await _identityContext.Set<DeliveryIncident>().CountAsync(cancellationToken):N0}.");
            facts.AppendLine($"Auditorías totales: {await _identityContext.Set<AuditLog>().CountAsync(cancellationToken):N0}.");

            var recentOrders = await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                .Include(order => order.Store)
                .OrderByDescending(order => order.CreatedAt)
                .Take(5)
                .Select(order => new
                {
                    order.DeliveryOrderId,
                    Store = order.Store.Name,
                    order.CustomerEmail,
                    order.Status,
                    order.Total
                })
                .ToListAsync(cancellationToken);
            foreach (var order in recentOrders)
                facts.AppendLine($"Pedido #{order.DeliveryOrderId}: {order.Store}, {order.CustomerEmail}, {order.Status}, ${order.Total:0.00}.");
        }
        else if (role == "Vendedor" && userId is not null)
        {
            var stores = await _identityContext.Set<DeliveryStore>().AsNoTracking()
                .Where(store => store.OwnerUserId == userId)
                .Select(store => new
                {
                    store.DeliveryStoreId,
                    store.Name,
                    store.Category,
                    Products = store.Products.Count(product => product.IsAvailable),
                    Orders = store.Orders.Count()
                })
                .ToListAsync(cancellationToken);
            facts.AppendLine(stores.Count == 0 ? "El vendedor no tiene tienda asignada." : "Tiendas propias:");
            foreach (var store in stores)
                facts.AppendLine($"Tienda #{store.DeliveryStoreId}: {store.Name}, {store.Category}, {store.Products:N0} productos, {store.Orders:N0} pedidos.");
        }
        else if (role == "Repartidor")
        {
            var deliveries = await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                .Include(order => order.Store)
                .Where(order => order.DeliveryPersonEmail == email)
                .OrderByDescending(order => order.CreatedAt)
                .Take(8)
                .Select(order => new { order.DeliveryOrderId, Store = order.Store.Name, order.Status, order.DeliveryAddress })
                .ToListAsync(cancellationToken);
            facts.AppendLine(deliveries.Count == 0 ? "El repartidor no tiene entregas asignadas." : "Entregas asignadas:");
            foreach (var delivery in deliveries)
                facts.AppendLine($"Pedido #{delivery.DeliveryOrderId}: {delivery.Store}, {delivery.Status}, {delivery.DeliveryAddress}.");
        }
        else if (role == "Usuario")
        {
            var orders = await _identityContext.Set<DeliveryOrder>().AsNoTracking()
                .Include(order => order.Store)
                .Where(order => order.CustomerEmail == email)
                .OrderByDescending(order => order.CreatedAt)
                .Take(8)
                .Select(order => new { order.DeliveryOrderId, Store = order.Store.Name, order.Status, order.Total })
                .ToListAsync(cancellationToken);
            facts.AppendLine(orders.Count == 0 ? "El usuario no tiene pedidos." : "Pedidos del usuario:");
            foreach (var order in orders)
                facts.AppendLine($"Pedido #{order.DeliveryOrderId}: {order.Store}, {order.Status}, ${order.Total:0.00}.");
        }

        if (Regex.IsMatch(message, "producto|precio|tienda|catálogo|catalogo|farmacia|supermercado|restaurante", RegexOptions.IgnoreCase))
        {
            var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "producto", "productos", "precio", "precios", "tienda", "tiendas", "catálogo", "catalogo",
                "cuanto", "cuántos", "cuantos", "donde", "disponible", "disponibles", "tienen", "tiene"
            };
            var searchTerm = Regex.Matches(message, @"[\p{L}\p{N}]+")
                .Select(match => match.Value)
                .Where(term => term.Length >= 4 && !ignored.Contains(term))
                .LastOrDefault();
            var productsQuery = _identityContext.Set<DeliveryProduct>().AsNoTracking()
                .Where(product => product.IsAvailable && product.Store.IsActive);
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var pattern = $"%{searchTerm}%";
                productsQuery = productsQuery.Where(product =>
                    EF.Functions.ILike(product.Name, pattern) ||
                    EF.Functions.ILike(product.Store.Name, pattern) ||
                    EF.Functions.ILike(product.Store.Category, pattern));
            }
            var products = await productsQuery
                .OrderBy(product => product.Store.Name)
                .ThenBy(product => product.Name)
                .Take(12)
                .Select(product => new { product.Name, Store = product.Store.Name, product.Store.Category, product.Price, product.Stock })
                .ToListAsync(cancellationToken);
            facts.AppendLine(products.Count == 0 ? "No hubo coincidencias en el catálogo." : "Coincidencias del catálogo:");
            foreach (var product in products)
                facts.AppendLine($"{product.Name}, {product.Store}, {product.Category}, ${product.Price:0.00}, stock {product.Stock:N0}.");
        }

        return facts.ToString().Trim();
    }

    private async Task<string?> BuildAuthoritativeAnswerAsync(
        string message,
        string role,
        string? userId,
        string fullName,
        CancellationToken cancellationToken)
    {
        var normalized = message.ToLowerInvariant();
        var parts = new List<string>();
        if (Regex.IsMatch(normalized, @"\b(nombre|quién soy|quien soy)\b"))
            parts.Add($"Tu nombre es {fullName}.");
        if (Regex.IsMatch(normalized, @"\b(rol|permisos?)\b"))
            parts.Add($"Tu rol es {role}.");

        if (Regex.IsMatch(normalized, @"\b(cuántos|cuantos|cantidad|total)\b") &&
            Regex.IsMatch(normalized, @"\bpedidos?\b"))
        {
            int count;
            if (role == "Administrador")
                count = await _identityContext.Set<DeliveryOrder>().CountAsync(cancellationToken);
            else if (role == "Vendedor" && userId is not null)
                count = await _identityContext.Set<DeliveryOrder>().CountAsync(
                    order => order.Store.OwnerUserId == userId, cancellationToken);
            else if (role == "Repartidor")
                count = await _identityContext.Set<DeliveryOrder>().CountAsync(
                    order => order.DeliveryPersonEmail == User.Identity!.Name, cancellationToken);
            else
                count = await _identityContext.Set<DeliveryOrder>().CountAsync(
                    order => order.CustomerEmail == User.Identity!.Name, cancellationToken);
            parts.Add(role == "Administrador"
                ? $"Hay {count:N0} pedidos en total."
                : $"Tienes {count:N0} pedidos asociados a tu cuenta.");
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private async Task WriteChatEventAsync(string type, string content, CancellationToken cancellationToken)
    {
        await Response.WriteAsync(JsonSerializer.Serialize(new { type, content }) + "\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private static string GetAssistantContext(string? context) => context?.ToLowerInvariant() switch
    {
        "login" => "La persona está en el inicio de sesión. Ayuda con acceso, contraseña olvidada, ingreso con Google y creación de cuenta. Nunca solicites contraseñas.",
        "register" => "La persona está creando una cuenta. Explica los roles públicos: Usuario compra y revisa pedidos; Vendedor tiene perfil comercial; Repartidor gestiona entregas. Administrador no está disponible en el registro público. Recuerda los requisitos de contraseña cuando sea útil.",
        "logout" => "La persona está cerrando sesión o ya salió. Ayuda a confirmar la salida, volver al inicio o ingresar con otra cuenta.",
        "administrador" => "La persona tiene rol Administrador. Ayuda con tiendas, productos, pedidos, estados, usuarios, roles y el panel administrativo.",
        "vendedor" => "La persona tiene rol Vendedor. Prioriza consultas sobre tiendas, catálogo, productos y operación comercial.",
        "repartidor" => "La persona tiene rol Repartidor. Ayuda a consultar entregas asignadas y actualizar estados de entrega.",
        "usuario" => "La persona tiene rol Usuario. Ayuda a explorar tiendas y productos, crear pedidos y revisar su estado.",
        _ => "La persona está en la página pública de Dash. Explica brevemente qué ofrece la aplicación, cómo ingresar o registrarse y qué tiendas o productos están disponibles."
    };

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public async Task<IActionResult> TableCounts()
    {
        var counts = new List<object>();
        long total = 0;

        var tasks = new (string Label, Func<Task<int>> Query)[]
        {
            ("Detalles de pedidos", () => _identityContext.Set<DeliveryOrderItem>().CountAsync()),
            ("Pedidos", () => _identityContext.Set<DeliveryOrder>().CountAsync()),
            ("Productos", () => _identityContext.Set<DeliveryProduct>().CountAsync()),
            ("Perfiles de usuario", () => _identityContext.Set<UserProfile>().CountAsync()),
            ("Pagos", () => _identityContext.Set<DeliveryPayment>().CountAsync()),
            ("Movimientos de inventario", () => _identityContext.Set<InventoryMovement>().CountAsync()),
            ("Auditorías", () => _identityContext.Set<AuditLog>().CountAsync()),
            ("Incidencias de entrega", () => _identityContext.Set<DeliveryIncident>().CountAsync()),
            ("Tiendas", () => _identityContext.Set<DeliveryStore>().CountAsync()),
            ("Productos en carritos", () => _identityContext.Set<DeliveryCartItem>().CountAsync()),
            ("Correos en cola", () => _identityContext.Set<EmailQueueItem>().CountAsync()),
            ("Reservas de stock", () => _identityContext.Set<StockReservation>().CountAsync()),
            ("Historial de estados", () => _identityContext.Set<OrderStatusHistory>().CountAsync()),
            ("Ciudades de Ecuador", () => _identityContext.Set<EcuadorCity>().CountAsync()),
            ("Provincias de Ecuador", () => _identityContext.Set<EcuadorProvince>().CountAsync()),
        };

        foreach (var (label, query) in tasks)
        {
            try
            {
                var count = await query();
                if (count > 0)
                {
                    counts.Add(new { table = label, count });
                    total += count;
                }
            }
            catch { }
        }

        return Json(new { items = counts, total });
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public async Task<IActionResult> MonthlySalesAnalytics()
    {
        // Ecuador continental uses UTC-5 throughout the year.
        var ecuadorNow = DateTime.UtcNow.AddHours(-5);
        var monthStartUtc = new DateTime(
            ecuadorNow.Year, ecuadorNow.Month, 1, 5, 0, 0, DateTimeKind.Utc);
        var nextMonthUtc = monthStartUtc.AddMonths(1);

        var monthlySales = _identityContext.Set<DeliveryOrder>().AsNoTracking()
            .Where(order =>
                order.CreatedAt >= monthStartUtc &&
                order.CreatedAt < nextMonthUtc &&
                order.Status != "Pendiente" &&
                order.Status != "Cancelado");

        var provinceSales = await monthlySales
            .Where(order => order.Store.Province != null)
            .GroupBy(order => order.Store.Province!.Name)
            .Select(group => new { Label = group.Key, Value = group.Sum(order => order.Total) })
            .ToListAsync();
        var provinceTotals = provinceSales.ToDictionary(item => item.Label, item => item.Value);
        var provinceNames = await _identityContext.Set<EcuadorProvince>().AsNoTracking()
            .Select(province => province.Name)
            .ToListAsync();
        var provinces = provinceNames
            .Select(label => new { Label = label, Value = provinceTotals.GetValueOrDefault(label, 0m) })
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Label)
            .Take(5)
            .ToList();

        var categorySales = await _identityContext.Set<DeliveryOrderItem>().AsNoTracking()
            .Where(item =>
                item.Order.CreatedAt >= monthStartUtc &&
                item.Order.CreatedAt < nextMonthUtc &&
                item.Order.Status != "Pendiente" &&
                item.Order.Status != "Cancelado" &&
                (item.Order.Store.Category == "Restaurantes" ||
                 item.Order.Store.Category == "Farmacia" ||
                 item.Order.Store.Category == "Supermercado"))
            .GroupBy(item => item.Order.Store.Category)
            .Select(group => new { Label = group.Key, Value = group.Sum(item => item.Quantity) })
            .ToListAsync();
        var categoryTotals = categorySales.ToDictionary(item => item.Label, item => item.Value);
        var categoryNames = await _identityContext.Set<DeliveryStore>().AsNoTracking()
            .Where(store =>
                store.Category == "Restaurantes" ||
                store.Category == "Farmacia" ||
                store.Category == "Supermercado")
            .Select(store => store.Category)
            .Distinct()
            .ToListAsync();
        var categories = categoryNames
            .Select(label => new { Label = label, Value = categoryTotals.GetValueOrDefault(label, 0) })
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Label)
            .Take(5)
            .ToList();

        var storeSales = await monthlySales
            .GroupBy(order => order.Store.Name)
            .Select(group => new { Label = group.Key, Value = group.Sum(order => order.Total) })
            .ToListAsync();
        var storeTotals = storeSales.ToDictionary(item => item.Label, item => item.Value);
        var storeNames = await _identityContext.Set<DeliveryStore>().AsNoTracking()
            .Select(store => store.Name)
            .Distinct()
            .ToListAsync();
        var stores = storeNames
            .Select(label => new { Label = label, Value = storeTotals.GetValueOrDefault(label, 0m) })
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Label)
            .Take(5)
            .ToList();

        return Json(new
        {
            periodStart = monthStartUtc,
            provinces,
            categories,
            stores
        });
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public IActionResult BackupStatus()
    {
        var now = DateTime.UtcNow;
        var next = _backups.NextBackupUtc;
        var remaining = (next - now).TotalSeconds;
        if (remaining < 0) remaining = 0;
        var backups = _backups.GetBackups();
        return Json(new
        {
            nextBackupUtc = next.ToString("o"),
            secondsRemaining = Math.Round(remaining, 1),
            backups = backups.Select((file, index) => new
            {
                number = index + 1,
                fileName = file.FileName,
                sizeBytes = file.SizeBytes,
                backupUtc = file.LastWriteTimeUtc.ToString("o")
            })
        });
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreBackup(string backupName, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _backups.RestoreAsync(backupName, cancellationToken);
            return Json(new
            {
                success = true,
                message = $"Backup {result.FileName} recuperado correctamente.",
                backupUtc = result.BackupUtc.ToString("o")
            });
        }
        catch (OperationCanceledException)
        {
            return BadRequest(new { success = false, message = "La recuperación fue cancelada." });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Database backup restore failed.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, message = "No se pudo recuperar la copia de seguridad." });
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> RoleCharts()
    {
        var email = User.Identity?.Name ?? "";

        if (User.IsInRole("Vendedor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var myStoreIds = await _identityContext.Set<DeliveryProduct>()
                .AsNoTracking()
                .Where(p => p.CreatedByUserId == userId)
                .Select(p => p.DeliveryStoreId)
                .Distinct()
                .ToListAsync();

            var ordersByStatus = await _identityContext.Set<DeliveryOrder>()
                .AsNoTracking()
                .Where(o => myStoreIds.Contains(o.DeliveryStoreId))
                .GroupBy(o => o.Status)
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToListAsync();

            var revenueByStatus = await _identityContext.Set<DeliveryOrder>()
                .AsNoTracking()
                .Where(o => myStoreIds.Contains(o.DeliveryStoreId))
                .GroupBy(o => o.Status)
                .Select(g => new { label = g.Key, count = (int)g.Sum(o => o.Total) })
                .OrderByDescending(g => g.count)
                .ToListAsync();

            var totalOrders = ordersByStatus.Sum(o => o.count);
            var totalRevenue = revenueByStatus.Sum(o => o.count);

            return Json(new
            {
                chart1Title = "Pedidos por estado",
                chart1 = ordersByStatus,
                chart1Total = totalOrders,
                chart2Title = "Ingresos por estado",
                chart2 = revenueByStatus,
                chart2Total = totalRevenue
            });
        }

        if (User.IsInRole("Repartidor"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var assigned = await _identityContext.Set<DeliveryOrder>()
                .AsNoTracking()
                .Where(o => o.DeliveryPersonEmail == email)
                .GroupBy(o => o.Status)
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToListAsync();

            var incidents = await _identityContext.Set<DeliveryIncident>()
                .AsNoTracking()
                .Where(i => i.ReportedByUserId == userId)
                .GroupBy(i => i.Severity)
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToListAsync();

            var totalAssigned = assigned.Sum(o => o.count);
            var totalIncidents = incidents.Sum(i => i.count);

            return Json(new
            {
                chart1Title = "Mis entregas",
                chart1 = assigned,
                chart1Total = totalAssigned,
                chart2Title = "Incidencias reportadas",
                chart2 = incidents,
                chart2Total = totalIncidents
            });
        }

        if (User.IsInRole("Usuario"))
        {
            var myOrders = await _identityContext.Set<DeliveryOrder>()
                .AsNoTracking()
                .Where(o => o.CustomerEmail == email)
                .GroupBy(o => o.Status)
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToListAsync();

            var myPayments = await _identityContext.Set<DeliveryPayment>()
                .AsNoTracking()
                .Where(p => p.Order.CustomerEmail == email)
                .GroupBy(p => p.Provider)
                .Select(g => new { label = g.Key, count = g.Count() })
                .OrderByDescending(g => g.count)
                .ToListAsync();

            var totalOrders = myOrders.Sum(o => o.count);
            var totalPayments = myPayments.Sum(p => p.count);

            return Json(new
            {
                chart1Title = "Mis pedidos",
                chart1 = myOrders,
                chart1Total = totalOrders,
                chart2Title = "Métodos de pago",
                chart2 = myPayments,
                chart2Total = totalPayments
            });
        }

        return Json(new
        {
            chart1Title = "Sin datos",
            chart1 = Array.Empty<object>(),
            chart1Total = 0,
            chart2Title = "Sin datos",
            chart2 = Array.Empty<object>(),
            chart2Total = 0
        });
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> PanelAdministrador()
    {
        var metrics = new List<(string Label, string Detail, int Value)>
        {
            ("Tiendas", "Tiendas registradas en Dash", await _identityContext.Set<DeliveryStore>().CountAsync()),
            ("Tiendas activas", "Tiendas disponibles para pedidos", await _identityContext.Set<DeliveryStore>().CountAsync(x => x.IsActive)),
            ("Productos", "Productos disponibles", await _identityContext.Set<DeliveryProduct>().CountAsync(x => x.IsAvailable)),
            ("Pedidos", "Pedidos creados", await _identityContext.Set<DeliveryOrder>().CountAsync()),
            ("En camino", "Entregas en curso", await _identityContext.Set<DeliveryOrder>().CountAsync(x => x.Status == "En camino")),
            ("Entregados", "Pedidos completados", await _identityContext.Set<DeliveryOrder>().CountAsync(x => x.Status == "Entregado")),
            ("Reservas", "Reservas de stock activas", await _identityContext.Set<StockReservation>().CountAsync(x => x.Status == "Activa")),
            ("Incidencias", "Incidencias abiertas", await _identityContext.Set<DeliveryIncident>().CountAsync(x => x.Status == "Abierto" || x.Status == "En revisión")),
            ("Correos", "Mensajes pendientes en cola", await _identityContext.Set<EmailQueueItem>().CountAsync(x => x.Status == "Pendiente")),
            ("Usuarios", "Cuentas de acceso", await _identityContext.Set<Microsoft.AspNetCore.Identity.IdentityUser>().CountAsync()),
            ("Roles", "Perfiles de Dash", await _identityContext.Set<Microsoft.AspNetCore.Identity.IdentityRole>().CountAsync())
        };

        var maxValue = metrics.Max(metric => metric.Value);
        var model = metrics
            .Select(metric => new AdminPanelMetric
            {
                Label = metric.Label,
                Detail = metric.Detail,
                Value = metric.Value,
                Percent = maxValue == 0 ? 0 : Math.Max(6, (int)Math.Round(metric.Value * 100.0 / maxValue))
            })
            .ToList();

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
