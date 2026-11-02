using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace OrbiApp.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/identity/auth")]
public sealed class IdentityAuthenticationApiController(
    SignInManager<IdentityUser> signInManager) : ControllerBase
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
}

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100)] string Password,
    bool RememberMe);
