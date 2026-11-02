using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models.Delivery;
using OrbiApp.Presentation.Client.Networking;

namespace OrbiApp.Controllers;

[ApiController]
[Authorize(Roles = "Usuario")]
[Route("api/delivery/payments")]
public sealed class DeliveryPaymentsApiController(IApplicationDataSession data) : ControllerBase
{
    private const int PageSize = 12;

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<DeliveryPaymentsResponse>> Get(
        [FromQuery] string? buscar,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { code = "authentication_required", message = "Inicia sesión para ver tus pagos." });

        var query = data.Set<DeliveryPayment>()
            .AsNoTracking()
            .Where(payment => payment.Order.CustomerEmail == email);

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var pattern = $"%{buscar.Trim()}%";
            query = query.Where(payment =>
                EF.Functions.ILike(payment.Provider, pattern) ||
                EF.Functions.ILike(payment.Status, pattern) ||
                EF.Functions.ILike(payment.Order.Store.Name, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
        var currentPage = Math.Clamp(page, 1, Math.Max(totalPages, 1));
        var payments = await query
            .OrderByDescending(payment => payment.CreatedAt)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(payment => new DeliveryPaymentSummary(
                payment.PaymentId,
                payment.DeliveryOrderId,
                payment.Order.Store.Name,
                payment.Provider,
                payment.Amount,
                payment.Status,
                payment.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new DeliveryPaymentsResponse(
            payments,
            totalCount,
            currentPage,
            totalPages,
            currentPage > 1,
            currentPage < totalPages));
    }
}
