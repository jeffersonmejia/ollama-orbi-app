using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Features.Identity.Client.Contracts;

namespace OrbiApp.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/identity/auth/session")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IdentitySessionApiController : ControllerBase
{
    [HttpGet]
    public ActionResult<IdentitySessionResponse> Get()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Ok(new IdentitySessionResponse(false, null, null, null, null, []));

        var identity = User.Identity as ClaimsIdentity;
        return Ok(new IdentitySessionResponse(
            true,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity.Name,
            User.FindFirstValue(ClaimTypes.Email),
            User.FindFirstValue(ClaimTypes.GivenName) ?? User.FindFirstValue("given_name"),
            User.FindAll(identity?.RoleClaimType ?? ClaimTypes.Role).Select(claim => claim.Value).Distinct().ToArray()));
    }
}
