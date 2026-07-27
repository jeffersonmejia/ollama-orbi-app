using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using SakilaApp.Data;
using SakilaApp.Models;
using SakilaApp.Services;

namespace SakilaApp.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _identityContext;
    private readonly OllamaProductService _ollama;

    public HomeController(ApplicationDbContext identityContext, OllamaProductService ollama)
    {
        _identityContext = identityContext;
        _ollama = ollama;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["FirstName"] = await _identityContext.UserProfiles
                .AsNoTracking()
                .Where(profile => profile.IdentityUserId == userId)
                .Select(profile => profile.FirstName)
                .FirstOrDefaultAsync();

            if (User.IsInRole("Administrador"))
            {
                var usersPerMonth = await _identityContext.UserProfiles
                    .AsNoTracking()
                    .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .ToListAsync();

                var ordersPerMonth = await _identityContext.DeliveryOrders
                    .AsNoTracking()
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.Status })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Status, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .ToListAsync();

                var storesPerMonth = await _identityContext.DeliveryStores
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
                ? (await _identityContext.UserProfiles.AsNoTracking()
                    .Where(p => p.CreatedAt >= since)
                    .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month, p.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.UserProfiles.AsNoTracking()
                    .Where(p => p.CreatedAt >= since)
                    .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month, p.CreatedAt.Day, p.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            var stores = isMonth
                ? (await _identityContext.DeliveryStores.AsNoTracking()
                    .Where(s => s.CreatedAt >= since)
                    .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month, s.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.DeliveryStores.AsNoTracking()
                    .Where(s => s.CreatedAt >= since)
                    .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month, s.CreatedAt.Day, s.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            return Json(new
            {
                Chart1Title = "Usuarios registrados",
                Chart1Color = "#C2185B",
                Chart1 = users.Select(u => new { Label = fmt(u.Hour, u.Month), Count = u.Count }),
                Chart1Total = users.Sum(u => u.Count),
                Chart2Title = "Tiendas registradas",
                Chart2Color = "#075c9b",
                Chart2 = stores.Select(s => new { Label = fmt(s.Hour, s.Month), Count = s.Count }),
                Chart2Total = stores.Sum(s => s.Count)
            });
        }

        if (User.IsInRole("Vendedor"))
        {
            var pedidos = isMonth
                ? (await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            var productos = isMonth
                ? (await _identityContext.DeliveryProducts.AsNoTracking().Include(p => p.Store)
                    .Where(p => p.Store.CreatedAt <= since)
                    .GroupBy(p => new { p.Store.CreatedAt.Year, p.Store.CreatedAt.Month, p.Store.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.DeliveryProducts.AsNoTracking().Include(p => p.Store)
                    .Where(p => p.Store.CreatedAt <= since)
                    .GroupBy(p => new { p.Store.CreatedAt.Year, p.Store.CreatedAt.Month, p.Store.CreatedAt.Day, p.Store.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            return Json(new
            {
                Chart1Title = "Pedidos recibidos",
                Chart1Color = "#B85700",
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
                ? (await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.DeliveryPersonEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.DeliveryPersonEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            var completadas = isMonth
                ? (await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.DeliveryPersonEmail == email && o.Status == "Entregado" && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.DeliveryOrders.AsNoTracking()
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
                Chart2Color = "#075c9b",
                Chart2 = completadas.Select(o => new { Label = fmt(o.Hour, o.Month), Count = o.Count }),
                Chart2Total = completadas.Sum(o => o.Count)
            });
        }

        if (User.IsInRole("Usuario"))
        {
            var misPedidos = isMonth
                ? (await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.Count() })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            var gasto = isMonth
                ? (await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Hour = 0, Total = g.Sum(o => o.Total) })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day)
                    .ToListAsync())
                : await _identityContext.DeliveryOrders.AsNoTracking()
                    .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                    .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Total = g.Sum(o => o.Total) })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                    .ToListAsync();

            return Json(new
            {
                Chart1Title = "Mis pedidos",
                Chart1Color = "#C2185B",
                Chart1 = misPedidos.Select(o => new { Label = fmt(o.Hour, o.Month), Count = o.Count }),
                Chart1Total = misPedidos.Sum(o => o.Count),
                Chart2Title = "Gasto acumulado",
                Chart2Color = "#B85700",
                Chart2 = gasto.Select(o => new { Label = fmt(o.Hour, o.Month), Count = (int)o.Total }),
                Chart2Total = (decimal)gasto.Sum(o => o.Total)
            });
        }

        return Json(new
        {
            Chart1Title = "Sin datos",
            Chart1Color = "#C2185B",
            Chart1 = Array.Empty<object>(),
            Chart1Total = 0,
            Chart2Title = "Sin datos",
            Chart2Color = "#075c9b",
            Chart2 = Array.Empty<object>(),
            Chart2Total = 0
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductChat(string message, string? context, CancellationToken cancellationToken)
    {
        message = message?.Trim() ?? string.Empty;
        if (message.Length is < 2 or > 250)
            return BadRequest(new { message = "Escribe una consulta de entre 2 y 250 caracteres." });

        var normalizedMessage = message.ToLowerInvariant();
        var contextKey = context?.ToLowerInvariant() ?? "public_home";

        var products = await _identityContext.DeliveryProducts
            .AsNoTracking()
            .Include(product => product.Store)
            .Where(product => product.IsAvailable && product.Store.IsActive)
            .OrderBy(product => product.Store.Name)
            .ThenBy(product => product.Name)
            .Select(product => new
            {
                Store = product.Store.Name,
                Category = product.Store.Category,
                Address = product.Store.Address,
                Product = product.Name,
                product.Price
            })
            .ToListAsync(cancellationToken);

        var catalog = products.Count == 0
            ? "No hay productos disponibles."
            : string.Join("\n", products.Select(item =>
                $"- Producto: {item.Product}; Tienda: {item.Store}; Categoría: {item.Category}; Dirección: {item.Address}; Precio: ${item.Price:0.00}; Disponible: sí"));

        var statsResult = await Task.WhenAll(
            _identityContext.DeliveryStores.CountAsync(),
            _identityContext.DeliveryProducts.CountAsync(),
            _identityContext.DeliveryOrders.CountAsync(),
            _identityContext.DeliveryPayments.CountAsync(),
            _identityContext.UserProfiles.CountAsync());
        var dbStats = $"Estadísticas: {statsResult[0]} tiendas, {statsResult[1]} productos, {statsResult[2]} pedidos, {statsResult[3]} pagos, {statsResult[4]} usuarios.";

        var assistantContext = GetAssistantContext(context) + "\nDATOS_REALES_DE_LA_APP: " + dbStats;

        try
        {
            var suggestion = await _ollama.SuggestAsync(message, catalog, assistantContext, cancellationToken);
            return Json(new { message = suggestion.Response });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "El asistente no está disponible. Verifica que Ollama esté encendido." });
        }
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
        _ => "La persona está en la página pública de Orbi. Explica brevemente qué ofrece la aplicación, cómo ingresar o registrarse y qué tiendas o productos están disponibles."
    };

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public async Task<IActionResult> TableCounts()
    {
        var counts = new List<object>();
        long total = 0;

        var tasks = new (string Name, Func<Task<int>> Query)[]
        {
            ("delivery_order_item", () => _identityContext.DeliveryOrderItems.CountAsync()),
            ("delivery_order", () => _identityContext.DeliveryOrders.CountAsync()),
            ("delivery_product", () => _identityContext.DeliveryProducts.CountAsync()),
            ("user_profile", () => _identityContext.UserProfiles.CountAsync()),
            ("payment", () => _identityContext.DeliveryPayments.CountAsync()),
            ("inventory_movement", () => _identityContext.InventoryMovements.CountAsync()),
            ("audit_log", () => _identityContext.AuditLogs.CountAsync()),
            ("delivery_incident", () => _identityContext.DeliveryIncidents.CountAsync()),
            ("delivery_store", () => _identityContext.DeliveryStores.CountAsync()),
            ("delivery_cart_item", () => _identityContext.DeliveryCartItems.CountAsync()),
            ("email_queue", () => _identityContext.EmailQueue.CountAsync()),
            ("stock_reservation", () => _identityContext.StockReservations.CountAsync()),
            ("order_status_history", () => _identityContext.OrderStatusHistories.CountAsync()),
            ("ecuador_city", () => _identityContext.EcuadorCities.CountAsync()),
            ("ecuador_province", () => _identityContext.EcuadorProvinces.CountAsync()),
        };

        foreach (var (name, query) in tasks)
        {
            try
            {
                var count = await query();
                if (count > 0)
                {
                    counts.Add(new { table = name, count });
                    total += count;
                }
            }
            catch { }
        }

        return Json(new { items = counts, total });
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public IActionResult BackupStatus()
    {
        var now = DateTime.UtcNow;
        var next = Services.BackupService.NextBackupUtc;
        var remaining = (next - now).TotalSeconds;
        if (remaining < 0) remaining = 0;
        return Json(new { nextBackupUtc = next.ToString("o"), secondsRemaining = Math.Round(remaining, 1) });
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> PanelAdministrador()
    {
        var metrics = new List<(string Label, string Detail, int Value)>
        {
            ("Tiendas", "Tiendas registradas en Orbi", await _identityContext.DeliveryStores.CountAsync()),
            ("Tiendas activas", "Tiendas disponibles para pedidos", await _identityContext.DeliveryStores.CountAsync(x => x.IsActive)),
            ("Productos", "Productos disponibles", await _identityContext.DeliveryProducts.CountAsync(x => x.IsAvailable)),
            ("Pedidos", "Pedidos creados", await _identityContext.DeliveryOrders.CountAsync()),
            ("En camino", "Entregas en curso", await _identityContext.DeliveryOrders.CountAsync(x => x.Status == "En camino")),
            ("Entregados", "Pedidos completados", await _identityContext.DeliveryOrders.CountAsync(x => x.Status == "Entregado")),
            ("Reservas", "Reservas de stock activas", await _identityContext.StockReservations.CountAsync(x => x.Status == "Activa")),
            ("Incidencias", "Incidencias abiertas", await _identityContext.DeliveryIncidents.CountAsync(x => x.Status == "Abierto" || x.Status == "En revisión")),
            ("Correos", "Mensajes pendientes en cola", await _identityContext.EmailQueue.CountAsync(x => x.Status == "Pendiente")),
            ("Usuarios", "Cuentas de acceso", await _identityContext.Users.CountAsync()),
            ("Roles", "Perfiles de Orbi", await _identityContext.Roles.CountAsync())
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
