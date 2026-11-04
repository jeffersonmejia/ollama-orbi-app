using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OrbiApp.Areas.Identity.Pages.Account.Manage;

[Authorize]
public sealed class ProfileIndexModel : PageModel
{
    public IActionResult OnGet() => Redirect("/Identity/Account/Manage");
}
