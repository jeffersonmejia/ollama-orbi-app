using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models.Identity;
using OrbiApp.Presentation.Client.Networking;
using OrbiApp.Presentation.Services;

namespace OrbiApp.Controllers;

[ApiController]
[Authorize]
[Route("api/identity/account/addresses")]
public sealed class IdentityAddressesApiController(
    IApplicationDataSession data,
    DeliveryPageCache pageCache) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IdentityAddressBookResponse>> Get(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "Inicia sesión para consultar tus direcciones." });

        await EnsurePrimaryAddressAsync(userId, cancellationToken);
        var addresses = await data.Set<UserAddress>()
            .AsNoTracking()
            .Where(address => address.IdentityUserId == userId)
            .Include(address => address.Province)
            .Include(address => address.City)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.Label)
            .ToListAsync(cancellationToken);
        var provinces = await pageCache.GetProvincesAsync(data, cancellationToken);
        var cities = await pageCache.GetCitiesAsync(data, cancellationToken);

        return Ok(new IdentityAddressBookResponse(
            addresses.Select(address => new IdentityAddressSummary(
                address.UserAddressId,
                address.Label,
                address.AddressLine1,
                address.AddressLine2,
                address.ProvinceCode,
                address.Province?.Name ?? string.Empty,
                address.CityCode,
                address.City?.Name ?? string.Empty,
                address.Reference,
                address.IsDefault,
                address.FormattedAddress)).ToArray(),
            provinces.Select(province => new IdentityProvinceOption(province.Code, province.Name)).ToArray(),
            cities.Select(city => new IdentityCityOption(city.Code, city.Name, city.ProvinceCode)).ToArray()));
    }

    [HttpPut]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IdentityAccountActionResponse>> Save(
        [FromBody] SaveIdentityAddressRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "Inicia sesión para guardar una dirección." });

        var label = request.Label.Trim();
        var validLocation = await data.Set<EcuadorCity>().AnyAsync(
            city => city.Code == request.CityCode && city.ProvinceCode == request.ProvinceCode,
            cancellationToken);
        if (!validLocation)
            return BadRequest(new { message = "La ciudad no pertenece a la provincia seleccionada." });

        var duplicateLabel = await data.Set<UserAddress>().AnyAsync(address =>
            address.IdentityUserId == userId &&
            address.Label.ToLower() == label.ToLower() &&
            address.UserAddressId != (request.UserAddressId ?? 0), cancellationToken);
        if (duplicateLabel)
            return Conflict(new { message = "Ya tienes una dirección con ese nombre." });

        UserAddress address;
        if (request.UserAddressId.HasValue)
        {
            var existingAddress = await data.Set<UserAddress>().SingleOrDefaultAsync(
                item => item.UserAddressId == request.UserAddressId.Value && item.IdentityUserId == userId,
                cancellationToken);
            if (existingAddress is null)
                return NotFound(new { message = "No se encontró la dirección de tu cuenta." });
            address = existingAddress;
        }
        else
        {
            address = new UserAddress { IdentityUserId = userId };
            data.Set<UserAddress>().Add(address);
        }

        var hasAddresses = await data.Set<UserAddress>().AnyAsync(item => item.IdentityUserId == userId, cancellationToken);
        address.Label = label;
        address.AddressLine1 = request.AddressLine1.Trim();
        address.AddressLine2 = request.AddressLine2.Trim();
        address.ProvinceCode = request.ProvinceCode;
        address.CityCode = request.CityCode;
        address.Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        address.IsDefault = request.IsDefault || !hasAddresses || address.IsDefault;
        address.UpdatedAt = DateTimeOffset.UtcNow;

        if (address.IsDefault)
        {
            await data.Set<UserAddress>()
                .Where(item => item.IdentityUserId == userId && item.UserAddressId != address.UserAddressId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false), cancellationToken);
            await SyncProfileAddressAsync(userId, address, cancellationToken);
        }

        await data.SaveChangesAsync(cancellationToken);
        pageCache.InvalidateUser(userId);
        return Ok(new IdentityAccountActionResponse(request.UserAddressId.HasValue ? "Dirección actualizada." : "Dirección agregada."));
    }

    [HttpPut("{userAddressId:long}/default")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IdentityAccountActionResponse>> SetDefault(
        long userAddressId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "Inicia sesión para actualizar tu dirección principal." });

        var address = await data.Set<UserAddress>().SingleOrDefaultAsync(
            item => item.UserAddressId == userAddressId && item.IdentityUserId == userId,
            cancellationToken);
        if (address is null)
            return NotFound(new { message = "No se encontró la dirección de tu cuenta." });

        await data.Set<UserAddress>()
            .Where(item => item.IdentityUserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false), cancellationToken);
        address.IsDefault = true;
        address.UpdatedAt = DateTimeOffset.UtcNow;
        await SyncProfileAddressAsync(userId, address, cancellationToken);
        await data.SaveChangesAsync(cancellationToken);
        pageCache.InvalidateUser(userId);
        return Ok(new IdentityAccountActionResponse($"{address.Label} ahora es tu dirección principal."));
    }

    [HttpDelete("{userAddressId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IdentityAccountActionResponse>> Delete(
        long userAddressId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "Inicia sesión para eliminar una dirección." });

        var address = await data.Set<UserAddress>().SingleOrDefaultAsync(
            item => item.UserAddressId == userAddressId && item.IdentityUserId == userId,
            cancellationToken);
        if (address is null)
            return NotFound(new { message = "No se encontró la dirección de tu cuenta." });
        if (address.IsDefault)
            return Conflict(new { message = "Elige otra dirección principal antes de eliminar esta." });

        data.Set<UserAddress>().Remove(address);
        await data.SaveChangesAsync(cancellationToken);
        pageCache.InvalidateUser(userId);
        return Ok(new IdentityAccountActionResponse("Dirección eliminada."));
    }

    private async Task EnsurePrimaryAddressAsync(string userId, CancellationToken cancellationToken)
    {
        if (await data.Set<UserAddress>().AnyAsync(item => item.IdentityUserId == userId, cancellationToken))
            return;

        var profile = await data.Set<UserProfile>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdentityUserId == userId, cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.AddressLine1))
            return;

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
    }

    private async Task SyncProfileAddressAsync(string userId, UserAddress address, CancellationToken cancellationToken)
    {
        var profile = await data.Set<UserProfile>()
            .SingleOrDefaultAsync(item => item.IdentityUserId == userId, cancellationToken);
        if (profile is null)
            return;

        profile.AddressLine1 = address.AddressLine1;
        profile.AddressLine2 = address.AddressLine2;
        profile.ProvinceCode = address.ProvinceCode;
        profile.CityCode = address.CityCode;
        profile.Reference = address.Reference;
    }
}
