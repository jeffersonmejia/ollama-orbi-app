using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models;
using OrbiApp.Models.Delivery;
using OrbiApp.Models.Identity;
using OrbiApp.Presentation.Client.Networking;
using OrbiApp.Presentation.Services;

namespace OrbiApp.Controllers;

[ApiController]
[Authorize]
[Route("api/delivery/catalog")]
public sealed class DeliveryCatalogApiController(
    IApplicationDataSession data,
    DeliveryPageCache pageCache) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<DeliveryCatalogResponse>> Get(
        [FromQuery] string? buscar,
        [FromQuery] decimal? precioMinimo,
        [FromQuery] decimal? precioMaximo,
        [FromQuery] string? categoria,
        [FromQuery] int page = 1,
        [FromQuery] string? cityCode = null,
        CancellationToken cancellationToken = default)
    {
        var isCustomer = User.IsInRole("Usuario");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var hasAddress = false;
        UserAddress? primaryAddress = null;
        UserProfile? profile = null;

        if (userId is not null)
        {
            profile = await pageCache.GetUserProfileAsync(data, userId, cancellationToken);
            primaryAddress = await pageCache.GetPrimaryAddressAsync(data, userId, cancellationToken);
            if (isCustomer && primaryAddress is null && profile is not null && !string.IsNullOrWhiteSpace(profile.AddressLine1))
            {
                data.Set<UserAddress>().Add(new UserAddress
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
                await data.SaveChangesAsync(cancellationToken);
                pageCache.InvalidateUser(userId);
                primaryAddress = await pageCache.GetPrimaryAddressAsync(data, userId, cancellationToken);
            }
            hasAddress = primaryAddress is not null;
        }

        var location = profile is not null
            ? new DeliveryCatalogLocation(profile.ProvinceCode, profile.Province?.Name, profile.CityCode, profile.City?.Name)
            : primaryAddress is not null
                ? new DeliveryCatalogLocation(primaryAddress.ProvinceCode, primaryAddress.Province?.Name, primaryAddress.CityCode, primaryAddress.City?.Name)
                : null;

        var productsQuery = data.Set<DeliveryProduct>()
            .AsNoTracking()
            .Include(product => product.Store).ThenInclude(store => store!.Province)
            .Include(product => product.Store).ThenInclude(store => store!.City)
            .Where(product => product.IsAvailable && product.Store.IsActive);

        var selectedCityCode = Request.Query.ContainsKey("cityCode")
            ? string.IsNullOrWhiteSpace(cityCode) ? null : cityCode
            : profile?.CityCode ?? primaryAddress?.CityCode;
        if (selectedCityCode is not null)
            productsQuery = productsQuery.Where(product => product.Store.CityCode == selectedCityCode);

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var pattern = $"%{buscar.Trim()}%";
            productsQuery = productsQuery.Where(product =>
                EF.Functions.ILike(product.Name, pattern) ||
                EF.Functions.ILike(product.Store.Name, pattern) ||
                EF.Functions.ILike(product.Store.Category, pattern));
        }

        if (precioMinimo.HasValue)
            productsQuery = productsQuery.Where(product => product.Price >= precioMinimo.Value);
        if (precioMaximo.HasValue)
            productsQuery = productsQuery.Where(product => product.Price <= precioMaximo.Value);
        if (!string.IsNullOrWhiteSpace(categoria))
            productsQuery = productsQuery.Where(product => product.Store.Category == categoria);

        var products = await PaginatedList<DeliveryProduct>.CreateAsync(
            productsQuery.OrderBy(product => product.Store.Name).ThenBy(product => product.Name),
            Math.Max(1, page),
            12);

        var cartQuantities = new Dictionary<int, int>();
        if (isCustomer && User.Identity?.Name is string email && products.Count > 0)
        {
            var productIds = products.Select(product => product.DeliveryProductId).ToArray();
            cartQuantities = await data.Set<DeliveryCartItem>()
                .AsNoTracking()
                .Where(item => item.UserEmail == email && productIds.Contains(item.DeliveryProductId))
                .ToDictionaryAsync(item => item.DeliveryProductId, item => item.Quantity, cancellationToken);
        }

        var response = new DeliveryCatalogResponse(
            products.Select(product => new DeliveryCatalogProduct(
                product.DeliveryProductId,
                product.Name,
                product.ImageUrl,
                product.Price,
                product.Stock,
                product.Store.Name,
                product.Store.Category,
                product.Store.Address,
                product.Store.City?.Name,
                cartQuantities.GetValueOrDefault(product.DeliveryProductId))).ToArray(),
            products.TotalCount,
            products.PageIndex,
            products.TotalPages,
            products.HasPreviousPage,
            products.HasNextPage,
            isCustomer,
            hasAddress,
            location,
            primaryAddress is null ? null : new DeliveryCatalogAddress(
                primaryAddress.UserAddressId,
                primaryAddress.Label,
                primaryAddress.AddressLine1,
                primaryAddress.AddressLine2,
                primaryAddress.ProvinceCode,
                primaryAddress.CityCode,
                primaryAddress.Reference ?? string.Empty));

        return Ok(response);
    }
}
