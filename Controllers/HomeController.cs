using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
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
    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProductChat(string message, CancellationToken cancellationToken)
    {
        message = message?.Trim() ?? string.Empty;
        if (message.Length is < 2 or > 300)
            return BadRequest(new { message = "Escribe una consulta de entre 2 y 300 caracteres." });

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

        if (products.Count == 0)
            return Json(new { message = "En este momento no hay productos disponibles." });

        var normalizedMessage = message.ToLowerInvariant();
        var asksForCatalog =
            (normalizedMessage.Contains("product") &&
             (normalizedMessage.Contains("disponib") || normalizedMessage.Contains("tiene") || normalizedMessage.Contains("hay"))) ||
            normalizedMessage.Contains("catálogo") ||
            normalizedMessage.Contains("catalogo") ||
            normalizedMessage.Contains("qué venden") ||
            normalizedMessage.Contains("que venden");

        if (asksForCatalog)
        {
            var productList = string.Join("\n", products.Select(item =>
                $"• {item.Product} — {item.Store} — ${item.Price:0.00}"));
            return Json(new { message = $"Estos son los productos disponibles:\n{productList}" });
        }

        var catalog = products.Count == 0
            ? "No hay productos disponibles."
            : string.Join("\n", products.Select(item =>
                $"- Producto: {item.Product}; Tienda: {item.Store}; Categoría: {item.Category}; Dirección: {item.Address}; Precio: ${item.Price:0.00}; Disponible: sí"));

        try
        {
            var answer = await _ollama.SuggestAsync(message, catalog, cancellationToken);
            return Json(new { message = answer });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "El asistente no está disponible. Verifica que Ollama esté encendido." });
        }
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
