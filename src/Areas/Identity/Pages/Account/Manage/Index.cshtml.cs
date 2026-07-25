#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SakilaApp.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public IndexModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string Username { get; set; }
    public string RoleSummary { get; set; }

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Phone(ErrorMessage = "Ingresa un número de teléfono válido.")]
        [Display(Name = "Teléfono")]
        public string PhoneNumber { get; set; }
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
        if (!ModelState.IsValid)
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

    private async Task LoadAsync(IdentityUser user)
    {
        Username = await _userManager.GetUserNameAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        RoleSummary = roles.Any() ? string.Join(", ", roles) : "Sin rol asignado";
        Input = new InputModel { PhoneNumber = await _userManager.GetPhoneNumberAsync(user) };
    }
}
