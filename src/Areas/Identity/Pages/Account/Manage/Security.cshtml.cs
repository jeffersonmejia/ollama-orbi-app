#nullable disable
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SakilaApp.Areas.Identity.Pages.Account.Manage
{
    public class SecurityModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IUserStore<IdentityUser> _userStore;

        public SecurityModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IUserStore<IdentityUser> userStore)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userStore = userStore;
        }

        public IList<UserLoginInfo> CurrentLogins { get; set; }
        public IList<AuthenticationScheme> OtherLogins { get; set; }
        public bool ShowRemoveButton { get; set; }
        public bool HasAuthenticator { get; set; }
        public int RecoveryCodesLeft { get; set; }
        public bool Is2faEnabled { get; set; }
        public bool IsMachineRemembered { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            CurrentLogins = await _userManager.GetLoginsAsync(user);
            OtherLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync())
                .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
                .ToList();

            string passwordHash = null;
            if (_userStore is IUserPasswordStore<IdentityUser> userPasswordStore)
                passwordHash = await userPasswordStore.GetPasswordHashAsync(user, HttpContext.RequestAborted);
            ShowRemoveButton = passwordHash != null || CurrentLogins.Count > 1;

            HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null;
            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user);
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);

            return Page();
        }

        public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
            if (!result.Succeeded)
            {
                StatusMessage = "No se pudo eliminar el acceso externo.";
                return RedirectToPage();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Acceso externo eliminado.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            var redirectUrl = Url.Page("./Security", pageHandler: "LinkLoginCallback");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, _userManager.GetUserId(User));
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var userId = await _userManager.GetUserIdAsync(user);
            var info = await _signInManager.GetExternalLoginInfoAsync(userId);
            if (info == null) throw new InvalidOperationException("Error al cargar información de acceso externo.");

            var result = await _userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                StatusMessage = "No se pudo agregar el acceso externo. Solo se puede asociar una cuenta por proveedor.";
                return RedirectToPage();
            }

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            StatusMessage = "Acceso externo agregado.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostForgetBrowserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            await _signInManager.ForgetTwoFactorClientAsync();
            StatusMessage = "Este navegador ha sido olvidado. Al iniciar sesión nuevamente se solicitará código de doble factor.";
            return RedirectToPage();
        }
    }
}
