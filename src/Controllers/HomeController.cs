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

        var email = User.Identity?.Name ?? "";

        if (User.IsInRole("Administrador"))
        {
            var users = await _identityContext.UserProfiles
                .AsNoTracking()
                .Where(p => p.CreatedAt >= since)
                .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month, p.CreatedAt.Day, p.CreatedAt.Hour })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                .ToListAsync();

            var stores = await _identityContext.DeliveryStores
                .AsNoTracking()
                .Where(s => s.CreatedAt >= since)
                .GroupBy(s => new { s.CreatedAt.Year, s.CreatedAt.Month, s.CreatedAt.Day, s.CreatedAt.Hour })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                .ToListAsync();

            return Json(new
            {
                Chart1Title = "Usuarios registrados",
                Chart1Color = "#C2185B",
                Chart1 = users.Select(u => new { Label = $"{u.Hour:D2}:00", Count = u.Count }),
                Chart1Total = users.Sum(u => u.Count),
                Chart2Title = "Tiendas registradas",
                Chart2Color = "#075c9b",
                Chart2 = stores.Select(s => new { Label = $"{s.Hour:D2}:00", Count = s.Count }),
                Chart2Total = stores.Sum(s => s.Count)
            });
        }

        if (User.IsInRole("Vendedor"))
        {
            var pedidos = await _identityContext.DeliveryOrders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= since)
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                .ToListAsync();

            var productos = await _identityContext.DeliveryProducts
                .AsNoTracking()
                .Include(p => p.Store)
                .Where(p => p.Store.CreatedAt <= since)
                .GroupBy(p => new { p.Store.CreatedAt.Year, p.Store.CreatedAt.Month, p.Store.CreatedAt.Day, p.Store.CreatedAt.Hour })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                .ToListAsync();

            return Json(new
            {
                Chart1Title = "Pedidos recibidos",
                Chart1Color = "#B85700",
                Chart1 = pedidos.Select(o => new { Label = $"{o.Hour:D2}:00", Count = o.Count }),
                Chart1Total = pedidos.Sum(o => o.Count),
                Chart2Title = "Productos en catálogo",
                Chart2Color = "#146c2e",
                Chart2 = productos.Select(p => new { Label = $"{p.Hour:D2}:00", Count = p.Count }),
                Chart2Total = productos.Sum(p => p.Count)
            });
        }

        if (User.IsInRole("Repartidor"))
        {
            var asignados = await _identityContext.DeliveryOrders
                .AsNoTracking()
                .Where(o => o.DeliveryPersonEmail == email && o.CreatedAt >= since)
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                .ToListAsync();

            var completadas = await _identityContext.DeliveryOrders
                .AsNoTracking()
                .Where(o => o.DeliveryPersonEmail == email && o.Status == "Entregado" && o.CreatedAt >= since)
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                .ToListAsync();

            return Json(new
            {
                Chart1Title = "Mis entregas",
                Chart1Color = "#146c2e",
                Chart1 = asignados.Select(o => new { Label = $"{o.Hour:D2}:00", Count = o.Count }),
                Chart1Total = asignados.Sum(o => o.Count),
                Chart2Title = "Completadas",
                Chart2Color = "#075c9b",
                Chart2 = completadas.Select(o => new { Label = $"{o.Hour:D2}:00", Count = o.Count }),
                Chart2Total = completadas.Sum(o => o.Count)
            });
        }

        if (User.IsInRole("Usuario"))
        {
            var misPedidos = await _identityContext.DeliveryOrders
                .AsNoTracking()
                .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                .ToListAsync();

            var gasto = await _identityContext.DeliveryOrders
                .AsNoTracking()
                .Where(o => o.CustomerEmail == email && o.CreatedAt >= since)
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Day = g.Key.Day, Hour = g.Key.Hour, Total = g.Sum(o => o.Total) })
                .OrderBy(g => g.Year).ThenBy(g => g.Month).ThenBy(g => g.Day).ThenBy(g => g.Hour)
                .ToListAsync();

            return Json(new
            {
                Chart1Title = "Mis pedidos",
                Chart1Color = "#C2185B",
                Chart1 = misPedidos.Select(o => new { Label = $"{o.Hour:D2}:00", Count = o.Count }),
                Chart1Total = misPedidos.Sum(o => o.Count),
                Chart2Title = "Gasto acumulado",
                Chart2Color = "#B85700",
                Chart2 = gasto.Select(o => new { Label = $"{o.Hour:D2}:00", Count = (int)o.Total }),
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
        if (message.Length is < 2 or > 300)
            return BadRequest(new { message = "Escribe una consulta de entre 2 y 300 caracteres." });

        var normalizedMessage = message.ToLowerInvariant();
        var contextKey = context?.ToLowerInvariant() ?? "public_home";
        var quickAnswer = GetQuickAnswer(contextKey, normalizedMessage);
        if (quickAnswer != null)
            return Json(new { message = quickAnswer });

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

        var asksForCatalog =
            (normalizedMessage.Contains("product") &&
             (normalizedMessage.Contains("disponib") || normalizedMessage.Contains("tiene") || normalizedMessage.Contains("hay"))) ||
            normalizedMessage.Contains("catálogo") ||
            normalizedMessage.Contains("catalogo") ||
            normalizedMessage.Contains("qué venden") ||
            normalizedMessage.Contains("que venden");

        if (asksForCatalog)
        {
            if (products.Count == 0)
                return Json(new { message = "En este momento no hay productos disponibles." });

            var productList = string.Join("\n", products.Select(item =>
                $"• {item.Product} — {item.Store} — ${item.Price:0.00}"));
            return Json(new { message = $"Estos son los productos disponibles:\n{productList}" });
        }

        var catalog = products.Count == 0
            ? "No hay productos disponibles."
            : string.Join("\n", products.Select(item =>
                $"- Producto: {item.Product}; Tienda: {item.Store}; Categoría: {item.Category}; Dirección: {item.Address}; Precio: ${item.Price:0.00}; Disponible: sí"));

        var assistantContext = GetAssistantContext(context);

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

    private static string? GetQuickAnswer(string context, string message)
    {
        if (context == "login")
        {
            if (message.Contains("olvid") && message.Contains("contraseña"))
                return "Selecciona “¿Olvidaste tu contraseña?” en el formulario de ingreso, escribe tu correo y sigue el enlace de recuperación que recibirás.";
            if (message.Contains("no puedo") || message.Contains("problema") || message.Contains("iniciar sesión"))
                return "Verifica que el correo y la contraseña estén bien escritos. Si aún no funciona, usa “¿Olvidaste tu contraseña?”; también puedes ingresar con Google o crear una cuenta nueva.";
            if (message.Contains("crear") && message.Contains("cuenta"))
                return "Selecciona “Registro” en la navegación. Allí podrás crear una cuenta como Usuario, Vendedor o Repartidor.";
        }

        if (context == "register")
        {
            if (message.Contains("rol"))
                return "Elige Usuario para comprar y revisar pedidos, Vendedor para el perfil comercial o Repartidor para gestionar entregas.";
            if (message.Contains("contraseña"))
                return "La contraseña debe tener al menos 6 caracteres e incluir mayúscula, minúscula y número.";
            if (message.Contains("administrador"))
                return "No. El rol Administrador no está disponible en el registro público; solo se crea mediante la configuración interna de Orbi.";
        }

        if (context == "logout")
        {
            if (message.Contains("cierro") || message.Contains("cerrar"))
                return "Usa el botón “Salir” de la navegación. Tu sesión se cerrará y volverás al inicio público.";
            if (message.Contains("cambiar") && message.Contains("cuenta"))
                return "Cierra la sesión actual con “Salir” y luego selecciona “Ingresar” para acceder con otra cuenta.";
            if (message.Contains("inicio"))
                return "Selecciona “Home” en la navegación para volver a la página principal.";
        }

        if (context == "administrador")
        {
            if (message.Contains("pedido")) return "Abre “Admin” en la navegación para revisar pedidos y actualizar sus estados.";
            if (message.Contains("rol")) return "Orbi tiene cuatro roles: Administrador, Vendedor, Repartidor y Usuario.";
            if (message.Contains("tienda")) return "Abre “Tiendas” para consultar el catálogo y “Admin” para supervisar la operación de pedidos.";
        }

        if (context == "vendedor")
        {
            if (message.Contains("puede hacer")) return "El perfil Vendedor está orientado a consultar tiendas, catálogo y productos disponibles para la operación comercial.";
            if (message.Contains("tienda") && message.Contains("activ")) return "Abre “Tiendas” para consultar las tiendas activas y sus productos.";
        }

        if (context == "repartidor")
        {
            if (message.Contains("dónde veo") || message.Contains("donde veo")) return "Abre “Entregas” en la navegación para ver los pedidos que tienes asignados.";
            if (message.Contains("marco") || message.Contains("actualiz")) return "En “Entregas”, abre el pedido asignado y usa la acción disponible para avanzar su estado.";
            if (message.Contains("estado")) return "Los estados operativos son En preparación, En camino y Entregado.";
        }

        if (context == "usuario")
        {
            if (message.Contains("hago un pedido")) return "Abre “Tiendas”, elige los productos que deseas y continúa con el proceso del pedido.";
            if (message.Contains("mis pedidos") || message.Contains("veo mis pedidos")) return "Abre “Pedidos” en la navegación para consultar tus pedidos y su estado.";
        }

        if (context == "public_home")
        {
            if (message.Contains("qué puedo hacer") || message.Contains("que puedo hacer")) return "En Orbi puedes explorar tiendas y productos, crear pedidos, seguir entregas y acceder a funciones específicas según tu rol.";
            if (message.Contains("crear") && message.Contains("cuenta")) return "Selecciona “Crear cuenta” y elige entre Usuario, Vendedor o Repartidor. El rol Administrador no se ofrece en el registro público.";
        }

        return null;
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
