using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OrbiApp.Application.Ports;
using OrbiApp.Presentation.Client.Networking;

namespace OrbiApp.Controllers;

[ApiController]
[Authorize]
[Route("api/identity/account")]
public sealed class IdentityAccountApiController(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IEmailSender emailSender,
    ILogger<IdentityAccountApiController> logger) : ControllerBase
{
    [HttpGet("profile")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IdentityProfileResponse>> GetProfile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return NotFound(new { message = "No se pudo cargar el usuario actual." });

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new IdentityProfileResponse(
            await userManager.GetUserNameAsync(user) ?? string.Empty,
            await userManager.GetEmailAsync(user) ?? string.Empty,
            await userManager.IsEmailConfirmedAsync(user),
            roles.Count > 0 ? string.Join(", ", roles) : "Sin rol asignado",
            await userManager.GetPhoneNumberAsync(user)));
    }

    [HttpPut("profile/phone")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IdentityAccountActionResponse>> UpdatePhone(
        [FromBody] UpdateIdentityPhoneRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return NotFound(new { message = "No se pudo cargar el usuario actual." });

        var result = await userManager.SetPhoneNumberAsync(user, request.PhoneNumber);
        if (!result.Succeeded)
            return BadRequest(new { message = "No se pudo actualizar el teléfono." });

        await signInManager.RefreshSignInAsync(user);
        return Ok(new IdentityAccountActionResponse("Perfil actualizado correctamente."));
    }

    [HttpPost("profile/email-change")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IdentityAccountActionResponse>> ChangeEmail(
        [FromBody] ChangeIdentityEmailRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return NotFound(new { message = "No se pudo cargar el usuario actual." });

        var currentEmail = await userManager.GetEmailAsync(user);
        if (string.Equals(request.NewEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "El correo ingresado ya está asociado a tu cuenta." });

        var userId = await userManager.GetUserIdAsync(user);
        var code = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "/Account/ConfirmEmailChange",
            pageHandler: null,
            values: new { area = "Identity", userId, email = request.NewEmail, code },
            protocol: Request.Scheme);
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            logger.LogError("Could not create the email change confirmation link for user {UserId}.", userId);
            return Problem("No se pudo preparar el correo de confirmación.");
        }

        await emailSender.SendEmailAsync(
            request.NewEmail,
            "Confirma tu nuevo correo",
            $"Confirma el cambio de correo <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>desde este enlace</a>.");

        return Accepted(new IdentityAccountActionResponse("Enviamos un enlace de confirmación al nuevo correo."));
    }

    [HttpPost("profile/verification-email")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IdentityAccountActionResponse>> SendVerificationEmail()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return NotFound(new { message = "No se pudo cargar el usuario actual." });
        if (await userManager.IsEmailConfirmedAsync(user))
            return Conflict(new { message = "El correo de esta cuenta ya está confirmado." });

        var userId = await userManager.GetUserIdAsync(user);
        var code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { area = "Identity", userId, code },
            protocol: Request.Scheme);
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            logger.LogError("Could not create the email confirmation link for user {UserId}.", userId);
            return Problem("No se pudo preparar el correo de confirmación.");
        }

        await emailSender.SendEmailAsync(
            await userManager.GetEmailAsync(user) ?? string.Empty,
            "Confirma tu correo",
            $"Confirma tu correo <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>desde este enlace</a>.");

        return Accepted(new IdentityAccountActionResponse("Enviamos un correo de verificación a tu dirección."));
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        logger.LogInformation("User logged out through the account API.");
        return NoContent();
    }
}
