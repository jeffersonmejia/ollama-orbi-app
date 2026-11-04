using Microsoft.EntityFrameworkCore;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models;
using OrbiApp.Models.Delivery;
using OrbiApp.Models.Operations;

namespace OrbiApp.Presentation.Features.Home;

public static class AdminDashboardMetricsQuery
{
    public static async Task<IReadOnlyList<AdminPanelMetric>> GetAsync(
        IApplicationDataSession data,
        CancellationToken cancellationToken = default)
    {
        var metrics = new List<(string Label, string Detail, int Value)>
        {
            ("Tiendas", "Tiendas registradas en Dash", await data.Set<DeliveryStore>().CountAsync(cancellationToken)),
            ("Tiendas activas", "Tiendas disponibles para pedidos", await data.Set<DeliveryStore>().CountAsync(x => x.IsActive, cancellationToken)),
            ("Productos", "Productos disponibles", await data.Set<DeliveryProduct>().CountAsync(x => x.IsAvailable, cancellationToken)),
            ("Pedidos", "Pedidos creados", await data.Set<DeliveryOrder>().CountAsync(cancellationToken)),
            ("En camino", "Entregas en curso", await data.Set<DeliveryOrder>().CountAsync(x => x.Status == "En camino", cancellationToken)),
            ("Entregados", "Pedidos completados", await data.Set<DeliveryOrder>().CountAsync(x => x.Status == "Entregado", cancellationToken)),
            ("Reservas", "Reservas de stock activas", await data.Set<StockReservation>().CountAsync(x => x.Status == "Activa", cancellationToken)),
            ("Incidencias", "Incidencias abiertas", await data.Set<DeliveryIncident>().CountAsync(x => x.Status == "Abierto" || x.Status == "En revisión", cancellationToken)),
            ("Correos", "Mensajes pendientes en cola", await data.Set<EmailQueueItem>().CountAsync(x => x.Status == "Pendiente", cancellationToken)),
            ("Usuarios", "Cuentas de acceso", await data.Set<Microsoft.AspNetCore.Identity.IdentityUser>().CountAsync(cancellationToken)),
            ("Roles", "Perfiles de Dash", await data.Set<Microsoft.AspNetCore.Identity.IdentityRole>().CountAsync(cancellationToken))
        };

        var maxValue = metrics.Max(metric => metric.Value);
        return metrics.Select(metric => new AdminPanelMetric
        {
            Label = metric.Label,
            Detail = metric.Detail,
            Value = metric.Value,
            Percent = maxValue == 0 ? 0 : Math.Max(6, (int)Math.Round(metric.Value * 100.0 / maxValue))
        }).ToArray();
    }
}
