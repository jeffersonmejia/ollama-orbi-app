// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SakilaApp.Data;
using SakilaApp.Models.Identity;

namespace SakilaApp.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private static readonly string[] PublicRoles = { "Usuario", "Vendedor", "Repartidor" };

        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _roleManager = roleManager;
            _context = context;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public IEnumerable<SelectListItem> AvailableRoles { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> AvailableProvinces { get; set; } = Enumerable.Empty<SelectListItem>();
        public string CitiesByProvinceJson { get; set; } = "{}";

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            [Required(ErrorMessage = "Los nombres son obligatorios.")]
            [StringLength(80, ErrorMessage = "Los nombres no pueden superar los {1} caracteres.")]
            [Display(Name = "Nombres")]
            public string FirstName { get; set; }

            [Required(ErrorMessage = "Los apellidos son obligatorios.")]
            [StringLength(80, ErrorMessage = "Los apellidos no pueden superar los {1} caracteres.")]
            [Display(Name = "Apellidos")]
            public string LastName { get; set; }

            [Required(ErrorMessage = "La cédula es obligatoria.")]
            [RegularExpression(@"^\d{10}$", ErrorMessage = "La cédula debe contener exactamente 10 dígitos.")]
            [Display(Name = "Cédula")]
            public string Cedula { get; set; }

            [Required(ErrorMessage = "La calle principal es obligatoria.")]
            [StringLength(160, ErrorMessage = "La calle principal no puede superar los {1} caracteres.")]
            [Display(Name = "Calle principal")]
            public string AddressLine1 { get; set; }

            [Required(ErrorMessage = "La calle secundaria es obligatoria.")]
            [StringLength(160, ErrorMessage = "La calle secundaria no puede superar los {1} caracteres.")]
            [Display(Name = "Calle secundaria")]
            public string AddressLine2 { get; set; }

            [Required(ErrorMessage = "Selecciona una provincia.")]
            [Display(Name = "Provincia")]
            public string ProvinceCode { get; set; }

            [Required(ErrorMessage = "Selecciona una ciudad.")]
            [Display(Name = "Ciudad")]
            public string CityCode { get; set; }

            [StringLength(240, ErrorMessage = "La referencia no puede superar los {1} caracteres.")]
            [Display(Name = "Referencia (opcional)")]
            public string Reference { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
            [EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido.")]
            [Display(Name = "Correo electrónico")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required(ErrorMessage = "La contraseña es obligatoria.")]
            [StringLength(100, ErrorMessage = "La {0} debe tener entre {2} y {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "contraseña")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirmar contraseña")]
            [Compare("Password", ErrorMessage = "La contraseña y su confirmación no coinciden.")]
            public string ConfirmPassword { get; set; }

            [Required(ErrorMessage = "Seleccione un rol para el usuario.")]
            [Display(Name = "Rol")]
            public string Role { get; set; }
        }


        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            Input ??= new InputModel();
            Input.Role ??= "Usuario";
            LoadRoles();
            await LoadLocationsAsync();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            LoadRoles();
            await LoadLocationsAsync();

            if (!string.IsNullOrWhiteSpace(Input?.Role) &&
                (!PublicRoles.Contains(Input.Role) || !await _roleManager.RoleExistsAsync(Input.Role)))
            {
                ModelState.AddModelError("Input.Role", "El rol seleccionado no está permitido para el registro público.");
            }

            if (!string.IsNullOrWhiteSpace(Input?.Cedula) && !IsValidEcuadorianCedula(Input.Cedula))
            {
                ModelState.AddModelError("Input.Cedula", "La cédula ecuatoriana no es válida.");
            }

            if (!string.IsNullOrWhiteSpace(Input?.Cedula) &&
                await _context.UserProfiles.AnyAsync(profile => profile.Cedula == Input.Cedula))
            {
                ModelState.AddModelError("Input.Cedula", "Ya existe una cuenta registrada con esta cédula.");
            }

            if (!string.IsNullOrWhiteSpace(Input?.ProvinceCode) &&
                !await _context.EcuadorProvinces.AnyAsync(province => province.Code == Input.ProvinceCode))
            {
                ModelState.AddModelError("Input.ProvinceCode", "La provincia seleccionada no es válida.");
            }

            if (!string.IsNullOrWhiteSpace(Input?.CityCode) &&
                !await _context.EcuadorCities.AnyAsync(city =>
                    city.Code == Input.CityCode && city.ProvinceCode == Input.ProvinceCode))
            {
                ModelState.AddModelError("Input.CityCode", "La ciudad no pertenece a la provincia seleccionada.");
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");
                    await _userManager.AddToRoleAsync(user, Input.Role);

                    var userId = await _userManager.GetUserIdAsync(user);
                    _context.UserProfiles.Add(new UserProfile
                    {
                        IdentityUserId = userId,
                        FirstName = Input.FirstName.Trim(),
                        LastName = Input.LastName.Trim(),
                        Cedula = Input.Cedula,
                        AddressLine1 = Input.AddressLine1.Trim(),
                        AddressLine2 = Input.AddressLine2.Trim(),
                        ProvinceCode = Input.ProvinceCode,
                        CityCode = Input.CityCode,
                        Reference = string.IsNullOrWhiteSpace(Input.Reference) ? null : Input.Reference.Trim()
                    });
                    _context.UserAddresses.Add(new UserAddress
                    {
                        IdentityUserId = userId,
                        Label = "Casa",
                        AddressLine1 = Input.AddressLine1.Trim(),
                        AddressLine2 = Input.AddressLine2.Trim(),
                        ProvinceCode = Input.ProvinceCode,
                        CityCode = Input.CityCode,
                        Reference = string.IsNullOrWhiteSpace(Input.Reference) ? null : Input.Reference.Trim(),
                        IsDefault = true
                    });

                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        await _userManager.DeleteAsync(user);
                        ModelState.AddModelError(string.Empty, "No fue posible guardar los datos del perfil. Verifica la cédula e inténtalo nuevamente.");
                        return Page();
                    }

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private void LoadRoles()
        {
            AvailableRoles = _roleManager.Roles
                .AsEnumerable()
                .Where(role => PublicRoles.Contains(role.Name))
                .OrderBy(role =>
                {
                    var index = Array.IndexOf(PublicRoles, role.Name);
                    return index >= 0 ? index : PublicRoles.Length;
                })
                .ThenBy(role => role.Name)
                .Select(role => new SelectListItem
                {
                    Value = role.Name,
                    Text = role.Name
                })
                .ToList();
        }

        private async Task LoadLocationsAsync()
        {
            var provinces = await _context.EcuadorProvinces
                .AsNoTracking()
                .OrderBy(province => province.Name)
                .ToListAsync();
            var cities = await _context.EcuadorCities
                .AsNoTracking()
                .OrderBy(city => city.Name)
                .ToListAsync();

            AvailableProvinces = provinces.Select(province => new SelectListItem
            {
                Value = province.Code,
                Text = province.Name
            });

            CitiesByProvinceJson = JsonSerializer.Serialize(
                cities.GroupBy(city => city.ProvinceCode)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(city => new CityOption(city.Code, city.Name)).ToArray()));
        }

        private static bool IsValidEcuadorianCedula(string cedula)
        {
            if (cedula.Length != 10 || !cedula.All(char.IsDigit)) return false;

            var province = int.Parse(cedula[..2]);
            if (province is < 1 or > 24 || cedula[2] > '5') return false;

            var sum = 0;
            for (var index = 0; index < 9; index++)
            {
                var value = (cedula[index] - '0') * (index % 2 == 0 ? 2 : 1);
                sum += value > 9 ? value - 9 : value;
            }

            var verifier = (10 - sum % 10) % 10;
            return verifier == cedula[9] - '0';
        }

        private sealed record CityOption(string Code, string Name);

        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}
