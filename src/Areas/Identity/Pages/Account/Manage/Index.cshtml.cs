#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SakilaApp.Data;
using SakilaApp.Models.Identity;

namespace SakilaApp.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public IndexModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    public string Username { get; set; }
    public string RoleSummary { get; set; }
    public IReadOnlyList<UserAddress> Addresses { get; set; } = Array.Empty<UserAddress>();
    public IReadOnlyList<EcuadorProvince> Provinces { get; set; } = Array.Empty<EcuadorProvince>();
    public IReadOnlyList<EcuadorCity> Cities { get; set; } = Array.Empty<EcuadorCity>();

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    [BindProperty]
    public AddressInputModel AddressInput { get; set; } = new();

    public class InputModel
    {
        [Phone(ErrorMessage = "Ingresa un número de teléfono válido.")]
        [Display(Name = "Teléfono")]
        public string PhoneNumber { get; set; }
    }

    public class AddressInputModel
    {
        public long? UserAddressId { get; set; }

        [Required(ErrorMessage = "Escribe un nombre breve para la dirección.")]
        [StringLength(40)]
        [Display(Name = "Nombre breve")]
        public string Label { get; set; } = "Casa";

        [Required(ErrorMessage = "La calle principal es obligatoria.")]
        [StringLength(160)]
        [Display(Name = "Calle principal")]
        public string AddressLine1 { get; set; }

        [StringLength(160)]
        [Display(Name = "Calle secundaria")]
        public string AddressLine2 { get; set; }

        [Required(ErrorMessage = "Selecciona una provincia.")]
        [Display(Name = "Provincia")]
        public string ProvinceCode { get; set; }

        [Required(ErrorMessage = "Selecciona una ciudad.")]
        [Display(Name = "Ciudad")]
        public string CityCode { get; set; }

        [StringLength(240)]
        [Display(Name = "Referencia")]
        public string Reference { get; set; }

        [Display(Name = "Usar como dirección principal")]
        public bool IsDefault { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(long? editAddressId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");

        await EnsurePrimaryAddressAsync(user.Id);
        await LoadAsync(user);

        if (editAddressId.HasValue)
        {
            var address = Addresses.SingleOrDefault(item => item.UserAddressId == editAddressId.Value);
            if (address is null) return NotFound();
            AddressInput = new AddressInputModel
            {
                UserAddressId = address.UserAddressId,
                Label = address.Label,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                ProvinceCode = address.ProvinceCode,
                CityCode = address.CityCode,
                Reference = address.Reference,
                IsDefault = address.IsDefault
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound("No se pudo cargar el usuario actual.");

        RemoveModelStatePrefix(nameof(AddressInput));
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

    public async Task<IActionResult> OnPostSaveAddressAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        RemoveModelStatePrefix(nameof(Input));
        var validLocation = await _context.EcuadorCities.AnyAsync(city =>
            city.Code == AddressInput.CityCode && city.ProvinceCode == AddressInput.ProvinceCode);
        if (!validLocation)
            ModelState.AddModelError("AddressInput.CityCode", "La ciudad no pertenece a la provincia seleccionada.");

        var normalizedLabel = AddressInput.Label?.Trim() ?? string.Empty;
        var duplicateLabel = await _context.UserAddresses.AnyAsync(item =>
            item.IdentityUserId == user.Id && item.Label.ToLower() == normalizedLabel.ToLower() &&
            item.UserAddressId != (AddressInput.UserAddressId ?? 0));
        if (duplicateLabel)
            ModelState.AddModelError("AddressInput.Label", "Ya tienes una dirección con ese nombre.");

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        UserAddress address;
        if (AddressInput.UserAddressId.HasValue)
        {
            address = await _context.UserAddresses.SingleOrDefaultAsync(item =>
                item.UserAddressId == AddressInput.UserAddressId && item.IdentityUserId == user.Id);
            if (address is null) return NotFound();
        }
        else
        {
            address = new UserAddress { IdentityUserId = user.Id };
            _context.UserAddresses.Add(address);
        }

        var hasAddresses = await _context.UserAddresses.AnyAsync(item => item.IdentityUserId == user.Id);
        address.Label = normalizedLabel;
        address.AddressLine1 = AddressInput.AddressLine1.Trim();
        address.AddressLine2 = AddressInput.AddressLine2?.Trim() ?? string.Empty;
        address.ProvinceCode = AddressInput.ProvinceCode;
        address.CityCode = AddressInput.CityCode;
        address.Reference = string.IsNullOrWhiteSpace(AddressInput.Reference) ? null : AddressInput.Reference.Trim();
        address.IsDefault = AddressInput.IsDefault || !hasAddresses || address.IsDefault;
        address.UpdatedAt = DateTimeOffset.UtcNow;

        if (address.IsDefault)
        {
            await _context.UserAddresses
                .Where(item => item.IdentityUserId == user.Id && item.UserAddressId != address.UserAddressId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false));
            await SyncProfileAddressAsync(user.Id, address);
        }

        await _context.SaveChangesAsync();
        StatusMessage = AddressInput.UserAddressId.HasValue ? "Dirección actualizada." : "Dirección agregada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetDefaultAsync(long id)
    {
        var userId = _userManager.GetUserId(User);
        var address = await _context.UserAddresses.SingleOrDefaultAsync(item => item.UserAddressId == id && item.IdentityUserId == userId);
        if (address is null) return NotFound();

        await _context.UserAddresses.Where(item => item.IdentityUserId == userId)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false));
        address.IsDefault = true;
        address.UpdatedAt = DateTimeOffset.UtcNow;
        await SyncProfileAddressAsync(userId, address);
        await _context.SaveChangesAsync();
        StatusMessage = $"{address.Label} ahora es tu dirección principal.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAddressAsync(long id)
    {
        var userId = _userManager.GetUserId(User);
        var address = await _context.UserAddresses.SingleOrDefaultAsync(item => item.UserAddressId == id && item.IdentityUserId == userId);
        if (address is null) return NotFound();
        if (address.IsDefault)
        {
            StatusMessage = "Error: elige otra dirección principal antes de eliminar esta.";
            return RedirectToPage();
        }

        _context.UserAddresses.Remove(address);
        await _context.SaveChangesAsync();
        StatusMessage = "Dirección eliminada.";
        return RedirectToPage();
    }

    private async Task LoadAsync(IdentityUser user)
    {
        Username = await _userManager.GetUserNameAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        RoleSummary = roles.Any() ? string.Join(", ", roles) : "Sin rol asignado";
        Input ??= new InputModel { PhoneNumber = await _userManager.GetPhoneNumberAsync(user) };
        Provinces = await _context.EcuadorProvinces.AsNoTracking().OrderBy(item => item.Name).ToListAsync();
        Cities = await _context.EcuadorCities.AsNoTracking().OrderBy(item => item.Name).ToListAsync();
        Addresses = await _context.UserAddresses.AsNoTracking()
            .Where(item => item.IdentityUserId == user.Id)
            .Include(item => item.Province).Include(item => item.City)
            .OrderByDescending(item => item.IsDefault).ThenBy(item => item.Label).ToListAsync();
    }

    private async Task EnsurePrimaryAddressAsync(string userId)
    {
        if (await _context.UserAddresses.AnyAsync(item => item.IdentityUserId == userId)) return;
        var profile = await _context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.IdentityUserId == userId);
        if (profile is null || string.IsNullOrWhiteSpace(profile.AddressLine1)) return;
        _context.UserAddresses.Add(new UserAddress
        {
            IdentityUserId = userId, Label = "Casa", AddressLine1 = profile.AddressLine1,
            AddressLine2 = profile.AddressLine2, ProvinceCode = profile.ProvinceCode,
            CityCode = profile.CityCode, Reference = profile.Reference, IsDefault = true
        });
        await _context.SaveChangesAsync();
    }

    private async Task SyncProfileAddressAsync(string userId, UserAddress address)
    {
        var profile = await _context.UserProfiles.SingleOrDefaultAsync(item => item.IdentityUserId == userId);
        if (profile is null) return;
        profile.AddressLine1 = address.AddressLine1;
        profile.AddressLine2 = address.AddressLine2;
        profile.ProvinceCode = address.ProvinceCode;
        profile.CityCode = address.CityCode;
        profile.Reference = address.Reference;
    }

    private void RemoveModelStatePrefix(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => key == prefix || key.StartsWith(prefix + ".", StringComparison.Ordinal)).ToList())
            ModelState.Remove(key);
    }
}
