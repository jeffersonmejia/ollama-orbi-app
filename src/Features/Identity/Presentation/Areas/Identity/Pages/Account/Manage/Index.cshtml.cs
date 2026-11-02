#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using OrbiApp.Application.Ports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace OrbiApp.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IEmailSender _emailSender;

    public IndexModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _emailSender = emailSender;
    }

    public string Username { get; set; }
    public string RoleSummary { get; set; }
    public string Email { get; set; }
    public bool IsEmailConfirmed { get; set; }

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    [BindProperty]
    public EmailInputModel EmailInput { get; set; }

    public class InputModel
    {
        [Phone(ErrorMessage = "Ingresa un número de teléfono válido.")]
        [Display(Name = "Teléfono")]
        public string PhoneNumber { get; set; }
    }

    public class EmailInputModel
    {
        [Required(ErrorMessage = "Ingresa el nuevo correo.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        [Display(Name = "Nuevo correo")]
        public string NewEmail { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");
        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            await LoadAsync(user);
            return Page();
        }

        if (Input.PhoneNumber != await _userManager.GetPhoneNumberAsync(user))
        {
            var result = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            if (!result.Succeeded)
            {
                StatusMessage = "Error: no se pudo actualizar el teléfono.";
                return RedirectToPage();
            }
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Perfil actualizado correctamente.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangeEmailAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");
        ModelState.Clear();
        if (!TryValidateModel(EmailInput, nameof(EmailInput)))
        {
            await LoadAsync(user);
            return Page();
        }

        var currentEmail = await _userManager.GetEmailAsync(user);
        if (EmailInput.NewEmail == currentEmail)
        {
            StatusMessage = "El correo ingresado ya está asociado a tu cuenta.";
            return RedirectToPage();
        }

        var userId = await _userManager.GetUserIdAsync(user);
        var code = await _userManager.GenerateChangeEmailTokenAsync(user, EmailInput.NewEmail);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page("/Account/ConfirmEmailChange", null,
            new { area = "Identity", userId, email = EmailInput.NewEmail, code }, Request.Scheme);
        await _emailSender.SendEmailAsync(EmailInput.NewEmail, "Confirma tu nuevo correo",
            $"Confirma el cambio de correo <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>desde este enlace</a>.");

        StatusMessage = "Enviamos un enlace de confirmación al nuevo correo.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendVerificationEmailAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");

        var userId = await _userManager.GetUserIdAsync(user);
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        var callbackUrl = Url.Page("/Account/ConfirmEmail", null,
            new { area = "Identity", userId, code }, Request.Scheme);
        await _emailSender.SendEmailAsync(await _userManager.GetEmailAsync(user), "Confirma tu correo",
            $"Confirma tu correo <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>desde este enlace</a>.");

        StatusMessage = "Enviamos un correo de verificación a tu dirección.";
        return RedirectToPage();
    }

    private async Task LoadAsync(IdentityUser user)
    {
        Username = await _userManager.GetUserNameAsync(user);
        Email = await _userManager.GetEmailAsync(user);
        IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        RoleSummary = roles.Any() ? string.Join(", ", roles) : "Sin rol asignado";
        Input = new InputModel { PhoneNumber = await _userManager.GetPhoneNumberAsync(user) };
        EmailInput ??= new EmailInputModel();
        EmailInput.NewEmail ??= Email;
    }
}
