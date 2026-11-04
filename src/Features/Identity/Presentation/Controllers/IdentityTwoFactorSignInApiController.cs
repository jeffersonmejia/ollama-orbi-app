using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Features.Identity.Client.Contracts;

namespace OrbiApp.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/identity/auth/two-factor")]
[RequestTimeout("api")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IdentityTwoFactorSignInApiController(
    SignInManager<IdentityUser> signInManager,
    ILogger<IdentityTwoFactorSignInApiController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TwoFactorChallengeResponse>> Get() =>
        Ok(new TwoFactorChallengeResponse(await signInManager.GetTwoFactorAuthenticationUserAsync() is not null));

    [HttpPost("authenticator")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Authenticator([FromBody] AuthenticatorSignInRequest request)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null) return ExpiredChallenge();
        var code = request.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);
        return Complete(await signInManager.TwoFactorAuthenticatorSignInAsync(code, request.RememberMe, request.RememberMachine));
    }

    [HttpPost("recovery-code")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecoveryCode([FromBody] RecoveryCodeSignInRequest request)
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null) return ExpiredChallenge();
        return Complete(await signInManager.TwoFactorRecoveryCodeSignInAsync(request.RecoveryCode.Replace(" ", string.Empty)));
    }

    private UnauthorizedObjectResult ExpiredChallenge() =>
        Unauthorized(new TwoFactorSignInError("La verificación venció. Vuelve a iniciar sesión."));

    private IActionResult Complete(Microsoft.AspNetCore.Identity.SignInResult result)
    {
        if (result.Succeeded)
        {
            logger.LogInformation("User completed two-factor sign-in.");
            return NoContent();
        }
        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, new TwoFactorSignInError("La cuenta está temporalmente bloqueada."));
        return Unauthorized(new TwoFactorSignInError("El código de verificación no es válido."));
    }
}
