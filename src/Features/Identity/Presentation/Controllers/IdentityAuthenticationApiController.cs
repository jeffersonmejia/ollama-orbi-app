using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;
using OrbiApp.Application.Ports;

namespace OrbiApp.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/identity/auth")]
public sealed class IdentityAuthenticationApiController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IEmailSender emailSender) : ControllerBase
{
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.Email,
            request.Password,
            request.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
            return NoContent();
        if (result.RequiresTwoFactor)
            return StatusCode(StatusCodes.Status428PreconditionRequired, new { code = "two_factor_required" });
        if (result.IsLockedOut)
            return StatusCode(StatusCodes.Status423Locked, new { code = "locked_out" });
        if (result.IsNotAllowed)
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "email_not_confirmed" });

        return Unauthorized(new { code = "invalid_credentials" });
    }

    [HttpPost("forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
            return NoContent();

        var code = await userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/Identity/Account/ResetPassword?code={Uri.EscapeDataString(code)}";

        await emailSender.SendEmailAsync(
            request.Email,
            "Reset Password",
            $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

        return NoContent();
    }

    [HttpPost("reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return NoContent();

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Code));
        }
        catch (FormatException)
        {
            return BadRequest(new { code = "invalid_reset_code", errors = new[] { "El enlace de restablecimiento no es válido o venció." } });
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.Password);
        if (result.Succeeded)
            return NoContent();

        return BadRequest(new
        {
            code = "password_reset_failed",
            errors = result.Errors.Select(error => error.Description).ToArray()
        });
    }
}

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100)] string Password,
    bool RememberMe);

public sealed record ForgotPasswordRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email);

public sealed record ResetPasswordRequest(
    [property: Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(100, MinimumLength = 6)] string Password,
    [property: Required, Compare("Password")] string ConfirmPassword,
    [property: Required] string Code);
