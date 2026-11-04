using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models.Delivery;
using OrbiApp.Presentation.Client.Networking;
using OrbiApp.Features.Identity.Presentation.Services;

namespace OrbiApp.Controllers;

[ApiController]
[Authorize(Roles = "Usuario")]
[Route("api/delivery/cart")]
public sealed class DeliveryCartApiController(
    IApplicationDataSession data,
    IdentityPageCache pageCache) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<DeliveryCartResponse>> Get(CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { code = "authentication_required", message = "Inicia sesión para ver tu carrito." });

        var items = await data.Set<DeliveryCartItem>()
            .AsNoTracking()
            .Include(item => item.Product)
            .ThenInclude(product => product.Store)
            .Where(item => item.UserEmail == email)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var responseItems = items.Select(item => new DeliveryCartItemResponse(
            item.DeliveryCartItemId,
            item.DeliveryProductId,
            item.Product?.Name ?? "Producto no disponible",
            item.Product?.Store?.Name ?? "—",
            item.Product?.Price ?? 0m,
            item.Quantity,
            (item.Product?.Price ?? 0m) * item.Quantity)).ToArray();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var hasAddress = userId is not null &&
            await pageCache.GetPrimaryAddressAsync(data, userId, cancellationToken) is not null;

        return Ok(new DeliveryCartResponse(
            responseItems,
            responseItems.Sum(item => item.Subtotal),
            hasAddress));
    }

    [HttpPut("items/{cartItemId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(
        long cartItemId,
        [FromBody] UpdateDeliveryCartQuantityRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity is < 1 or > 999)
            return BadRequest(new { code = "invalid_quantity", message = "La cantidad debe estar entre 1 y 999." });

        var email = User.Identity?.Name;
        var item = await data.Set<DeliveryCartItem>()
            .FirstOrDefaultAsync(cartItem => cartItem.DeliveryCartItemId == cartItemId && cartItem.UserEmail == email, cancellationToken);
        if (item is null)
            return NotFound(new { code = "cart_item_not_found", message = "El producto ya no está en el carrito." });

        item.Quantity = request.Quantity;
        await data.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("items/{cartItemId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(
        long cartItemId,
        CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        var item = await data.Set<DeliveryCartItem>()
            .FirstOrDefaultAsync(cartItem => cartItem.DeliveryCartItemId == cartItemId && cartItem.UserEmail == email, cancellationToken);
        if (item is null)
            return NotFound(new { code = "cart_item_not_found", message = "El producto ya no está en el carrito." });

        data.Set<DeliveryCartItem>().Remove(item);
        await data.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
