using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Features.Identity.Presentation.Services;
using OrbiApp.Models.Identity;
using OrbiApp.Presentation.Client;

namespace OrbiApp.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/identity/registration")]
public sealed class IdentityRegistrationOptionsApiController(
    IAntiforgery antiforgery,
    RoleManager<IdentityRole> roleManager,
    IApplicationDataSession data,
    IdentityPageCache pageCache) : ControllerBase
{
    [HttpGet("options")]
    [RequestTimeout("api")]
    public async Task<ActionResult<RegistrationOptionsResponse>> Get(CancellationToken cancellationToken)
    {
        var roles = await pageCache.GetPublicRegistrationRolesAsync(roleManager, cancellationToken);

        var provinces = (await pageCache.GetProvincesAsync(data, cancellationToken))
            .Select(province => new RegistrationProvinceOption(province.Code, province.Name))
            .ToList();
        var cities = (await pageCache.GetCitiesAsync(data, cancellationToken))
            .Select(city => new RegistrationCityOption(city.Code, city.Name, city.ProvinceCode))
            .ToList();

        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new RegistrationOptionsResponse(tokens.RequestToken ?? string.Empty, roles, provinces, cities));
    }
}
