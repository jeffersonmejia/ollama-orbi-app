using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Features.Identity.Client.Contracts;

namespace OrbiApp.Controllers;

[ApiController]
[Authorize]
[Route("api/identity/account/password")]
[RequestTimeout("api")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IdentityPasswordApiController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ILogger<IdentityPasswordApiController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IdentityPasswordStatusResponse>> Get()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return NotFound(new IdentityPasswordActionResponse("No se pudo cargar el usuario actual."));

        return Ok(new IdentityPasswordStatusResponse(await userManager.HasPasswordAsync(user)));
    }

    [HttpPost("change")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IdentityPasswordActionResponse>> Change([FromBody] ChangeIdentityPasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return NotFound(new IdentityPasswordActionResponse("No se pudo cargar el usuario actual."));

        var result = await userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
        if (!result.Succeeded)
            return PasswordError(result);

        await signInManager.RefreshSignInAsync(user);
        logger.LogInformation("User changed their password successfully.");
        return Ok(new IdentityPasswordActionResponse("Tu contraseña se ha cambiado."));
    }

    [HttpPost("set")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IdentityPasswordActionResponse>> Set([FromBody] SetIdentityPasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return NotFound(new IdentityPasswordActionResponse("No se pudo cargar el usuario actual."));

        var result = await userManager.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
            return PasswordError(result);

        await signInManager.RefreshSignInAsync(user);
        return Ok(new IdentityPasswordActionResponse("Tu contraseña se ha configurado."));
    }

    private BadRequestObjectResult PasswordError(IdentityResult result) =>
        BadRequest(new IdentityPasswordActionResponse(
            "No se pudo guardar la contraseña.", result.Errors.Select(error => error.Description).ToArray()));
}
