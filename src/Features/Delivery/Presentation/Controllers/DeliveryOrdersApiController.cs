using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models.Delivery;
using OrbiApp.Models.Operations;
using OrbiApp.Presentation.Client.Networking;

namespace OrbiApp.Controllers;

[ApiController]
[Authorize]
[Route("api/delivery")]
public sealed class DeliveryOrdersApiController(IApplicationDataSession data) : ControllerBase
{
    private const int PageSize = 5;
    private static readonly string[] RepartidorStatuses = ["En preparación", "En camino", "Entregado"];
    private string? CurrentEmail => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;

    [HttpGet("orders")]
    [Authorize(Roles = "Usuario")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<ActionResult<DeliveryOrdersResponse>> GetCustomerOrders(
        [FromQuery] string? buscar,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default) =>
        GetOrdersAsync(buscar, page, CurrentEmail, deliveryPerson: false, cancellationToken);

    [HttpGet("deliveries")]
    [Authorize(Roles = "Repartidor")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<ActionResult<DeliveryOrdersResponse>> GetAssignedDeliveries(
        [FromQuery] string? buscar,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default) =>
        GetOrdersAsync(buscar, page, CurrentEmail, deliveryPerson: true, cancellationToken);

    [HttpPut("deliveries/{orderId:int}/status")]
    [Authorize(Roles = "Repartidor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDeliveryStatus(
        int orderId,
        [FromBody] UpdateDeliveryOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!RepartidorStatuses.Contains(request.Status, StringComparer.Ordinal))
            return BadRequest(new { code = "invalid_order_status", message = "Selecciona un estado válido para la entrega." });

        var email = CurrentEmail;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { code = "authentication_required", message = "Inicia sesión para actualizar la entrega." });

        var order = await data.Set<DeliveryOrder>()
            .FirstOrDefaultAsync(item => item.DeliveryOrderId == orderId &&
                item.DeliveryPersonEmail != null && EF.Functions.ILike(item.DeliveryPersonEmail, email), cancellationToken);
        if (order is null)
            return NotFound(new { code = "delivery_not_found", message = "No se encontró la entrega asignada." });

        var previousStatus = order.Status;
        if (previousStatus == request.Status)
            return NoContent();

        order.Status = request.Status;
        data.Set<OrderStatusHistory>().Add(new OrderStatusHistory
        {
            DeliveryOrderId = order.DeliveryOrderId,
            ChangedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            PreviousStatus = previousStatus,
            NewStatus = request.Status
        });
        await data.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult<DeliveryOrdersResponse>> GetOrdersAsync(
        string? search,
        int requestedPage,
        string? email,
        bool deliveryPerson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { code = "authentication_required", message = "Inicia sesión para ver tus pedidos." });

        var query = data.Set<DeliveryOrder>()
            .AsNoTracking()
            .Include(order => order.Store)
            .Include(order => order.Items)
            .Where(order => deliveryPerson
                ? order.DeliveryPersonEmail != null && EF.Functions.ILike(order.DeliveryPersonEmail, email) && order.Status != "Cancelado"
                : EF.Functions.ILike(order.CustomerEmail, email));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var pattern = $"%{term}%";
            var hasOrderId = int.TryParse(term.TrimStart('#'), out var orderId);
            query = deliveryPerson
                ? query.Where(order =>
                    EF.Functions.ILike(order.Store.Name, pattern) ||
                    EF.Functions.ILike(order.Status, pattern) ||
                    EF.Functions.ILike(order.CustomerEmail, pattern) ||
                    order.Items.Any(item => EF.Functions.ILike(item.ProductName, pattern)) ||
                    (hasOrderId && order.DeliveryOrderId == orderId))
                : query.Where(order =>
                    EF.Functions.ILike(order.Store.Name, pattern) ||
                    EF.Functions.ILike(order.Status, pattern) ||
                    order.Items.Any(item => EF.Functions.ILike(item.ProductName, pattern)) ||
                    (hasOrderId && order.DeliveryOrderId == orderId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
        var page = Math.Clamp(requestedPage, 1, Math.Max(totalPages, 1));
        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(order => new DeliveryOrderSummary(
                order.DeliveryOrderId,
                order.CreatedAt,
                order.Store.Name,
                order.Items.OrderBy(item => item.DeliveryOrderItemId)
                    .Select(item => new DeliveryOrderItemSummary(item.Quantity, item.ProductName)).ToArray(),
                order.DeliveryAddress,
                order.Total,
                order.Status))
            .ToListAsync(cancellationToken);

        return Ok(new DeliveryOrdersResponse(
            orders,
            totalCount,
            page,
            totalPages,
            page > 1,
            page < totalPages));
    }
}
