using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using OrbiApp.Application.Ports;

namespace OrbiApp.Areas.Identity.Pages.Account.Manage;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileModel(
    UserManager<IdentityUser> userManager,
    IEmailSender emailSender,
    ILogger<ProfileModel> logger) : PageModel
{
    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string RoleSummary { get; private set; } = "Sin rol asignado";
    public bool IsEmailConfirmed { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadProfileAsync()) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostSavePhoneAsync()
    {
        ModelState.Remove("Input.NewEmail");
        if (!ModelState.IsValid)
        {
            if (!await LoadProfileAsync()) return NotFound();
            return Page();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var result = await userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "No se pudo guardar el teléfono.");
            if (!await LoadProfileAsync()) return NotFound();
            return Page();
        }

        StatusMessage = "Perfil actualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangeEmailAsync()
    {
        ModelState.Remove("Input.PhoneNumber");
        if (!ModelState.IsValid)
        {
            if (!await LoadProfileAsync()) return NotFound();
            return Page();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        var currentEmail = await userManager.GetEmailAsync(user);
        if (string.Equals(Input.NewEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Input.NewEmail", "El correo ingresado ya está asociado a tu cuenta.");
            if (!await LoadProfileAsync()) return NotFound();
            return Page();
        }

        var userId = await userManager.GetUserIdAsync(user);
        var code = await userManager.GenerateChangeEmailTokenAsync(user, Input.NewEmail);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page(
            "/Account/ConfirmEmailChange",
            pageHandler: null,
            values: new { area = "Identity", userId, email = Input.NewEmail, code },
            protocol: Request.Scheme);
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            logger.LogError("Could not create the email change confirmation link for user {UserId}.", userId);
            ErrorMessage = "No se pudo preparar el correo de confirmación.";
            if (!await LoadProfileAsync()) return NotFound();
            return Page();
        }

        await emailSender.SendEmailAsync(Input.NewEmail, "Confirma tu nuevo correo",
            $"Confirma el cambio de correo <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>desde este enlace</a>.");
        StatusMessage = "Enviamos un enlace de confirmación al nuevo correo.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendVerificationAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return NotFound();
        if (await userManager.IsEmailConfirmedAsync(user))
        {
            StatusMessage = "El correo de esta cuenta ya está confirmado.";
            return RedirectToPage();
        }

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
            StatusMessage = "No se pudo preparar el correo de verificación.";
            return RedirectToPage();
        }

        await emailSender.SendEmailAsync(await userManager.GetEmailAsync(user) ?? string.Empty, "Confirma tu correo",
            $"Confirma tu correo <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>desde este enlace</a>.");
        StatusMessage = "Enviamos un correo de verificación a tu dirección.";
        return RedirectToPage();
    }

    private async Task<bool> LoadProfileAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return false;

        Username = await userManager.GetUserNameAsync(user) ?? string.Empty;
        Email = await userManager.GetEmailAsync(user) ?? string.Empty;
        IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);
        var roles = await userManager.GetRolesAsync(user);
        RoleSummary = roles.Count > 0 ? string.Join(", ", roles) : "Sin rol asignado";
        Input.PhoneNumber ??= await userManager.GetPhoneNumberAsync(user);
        Input.NewEmail = string.Empty;
        return true;
    }

    public sealed class ProfileInput
    {
        [Phone(ErrorMessage = "Ingresa un número de teléfono válido.")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Ingresa el nuevo correo.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        public string NewEmail { get; set; } = string.Empty;
    }
}
