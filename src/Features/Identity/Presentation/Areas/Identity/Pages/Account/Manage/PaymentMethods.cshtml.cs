#nullable disable

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OrbiApp.Areas.Identity.Pages.Account.Manage;

public class PaymentMethodsModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    public PaymentMethodsModel(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");

        return Page();
    }
}
