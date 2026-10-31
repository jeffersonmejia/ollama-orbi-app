using OrbiApp.Application.Features.Shared.Ports;
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Models.Identity;

namespace OrbiApp.Areas.Identity.Pages.Account.Manage;

public class PaymentMethodsModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IApplicationDataSession _context;

    public PaymentMethodsModel(UserManager<IdentityUser> userManager, IApplicationDataSession context)
    {
        _userManager = userManager;
        _context = context;
    }

    public string CurrentMethod { get; set; }

    [TempData]
    public string StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");

        var profile = await _context.Set<UserProfile>().FirstOrDefaultAsync(p => p.IdentityUserId == user.Id);
        CurrentMethod = profile?.PreferredPaymentMethod ?? "PayPal";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string preferredPayment)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");

        var allowed = new[] { "PayPal", "PayPhone" };
        if (string.IsNullOrWhiteSpace(preferredPayment) || !allowed.Contains(preferredPayment))
        {
            StatusMessage = "Error: método de pago no válido.";
            return RedirectToPage();
        }

        var profile = await _context.Set<UserProfile>().FirstOrDefaultAsync(p => p.IdentityUserId == user.Id);
        if (profile is null)
        {
            StatusMessage = "Error: no se encontró el perfil del usuario.";
            return RedirectToPage();
        }

        profile.PreferredPaymentMethod = preferredPayment;
        await _context.SaveChangesAsync();

        StatusMessage = $"Método de pago actualizado a {preferredPayment}.";
        return RedirectToPage();
    }
}
