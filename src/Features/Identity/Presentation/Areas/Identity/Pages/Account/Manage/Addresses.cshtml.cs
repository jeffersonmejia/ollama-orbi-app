using OrbiApp.Application.Features.Shared.Ports;
#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Models.Identity;
using OrbiApp.Presentation.Services;

namespace OrbiApp.Areas.Identity.Pages.Account.Manage;

public class AddressesModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IApplicationDataSession _context;
    private readonly DeliveryPageCache _pageCache;

    public AddressesModel(UserManager<IdentityUser> userManager, IApplicationDataSession context, DeliveryPageCache pageCache)
    {
        _userManager = userManager;
        _context = context;
        _pageCache = pageCache;
    }

    public IReadOnlyList<UserAddress> Addresses { get; set; } = Array.Empty<UserAddress>();
    public IReadOnlyList<EcuadorProvince> Provinces { get; set; } = Array.Empty<EcuadorProvince>();
    public IReadOnlyList<EcuadorCity> Cities { get; set; } = Array.Empty<EcuadorCity>();

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty]
    public AddressInputModel AddressInput { get; set; } = new();

    public class AddressInputModel
    {
        public long? UserAddressId { get; set; }

        [Required(ErrorMessage = "Escribe un nombre breve.")]
        [StringLength(40)]
        [Display(Name = "Nombre")]
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

        [Display(Name = "Dirección principal")]
        public bool IsDefault { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(long? editAddressId)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null) return NotFound();
        await EnsurePrimaryAddressAsync(userId);
        await LoadAsync(userId);

        if (editAddressId.HasValue)
        {
            var address = Addresses.SingleOrDefault(item => item.UserAddressId == editAddressId.Value);
            if (address is null) return NotFound();
            AddressInput = new AddressInputModel
            {
                UserAddressId = address.UserAddressId, Label = address.Label,
                AddressLine1 = address.AddressLine1, AddressLine2 = address.AddressLine2,
                ProvinceCode = address.ProvinceCode, CityCode = address.CityCode,
                Reference = address.Reference, IsDefault = address.IsDefault
            };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAddressAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null) return NotFound();

        var validLocation = await _context.Set<EcuadorCity>().AnyAsync(city => city.Code == AddressInput.CityCode && city.ProvinceCode == AddressInput.ProvinceCode);
        if (!validLocation) ModelState.AddModelError("AddressInput.CityCode", "La ciudad no pertenece a la provincia seleccionada.");

        var label = AddressInput.Label?.Trim() ?? string.Empty;
        if (await _context.Set<UserAddress>().AnyAsync(item => item.IdentityUserId == userId && item.Label.ToLower() == label.ToLower() && item.UserAddressId != (AddressInput.UserAddressId ?? 0)))
            ModelState.AddModelError("AddressInput.Label", "Ya tienes una dirección con ese nombre.");

        if (!ModelState.IsValid)
        {
            await LoadAsync(userId);
            return Page();
        }

        UserAddress address;
        if (AddressInput.UserAddressId.HasValue)
        {
            address = await _context.Set<UserAddress>().SingleOrDefaultAsync(item => item.UserAddressId == AddressInput.UserAddressId && item.IdentityUserId == userId);
            if (address is null) return NotFound();
        }
        else
        {
            address = new UserAddress { IdentityUserId = userId };
            _context.Set<UserAddress>().Add(address);
        }

        var hasAddresses = await _context.Set<UserAddress>().AnyAsync(item => item.IdentityUserId == userId);
        address.Label = label;
        address.AddressLine1 = AddressInput.AddressLine1.Trim();
        address.AddressLine2 = AddressInput.AddressLine2?.Trim() ?? string.Empty;
        address.ProvinceCode = AddressInput.ProvinceCode;
        address.CityCode = AddressInput.CityCode;
        address.Reference = string.IsNullOrWhiteSpace(AddressInput.Reference) ? null : AddressInput.Reference.Trim();
        address.IsDefault = AddressInput.IsDefault || !hasAddresses || address.IsDefault;
        address.UpdatedAt = DateTimeOffset.UtcNow;

        if (address.IsDefault)
        {
            await _context.Set<UserAddress>().Where(item => item.IdentityUserId == userId && item.UserAddressId != address.UserAddressId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false));
            await SyncProfileAddressAsync(userId, address);
        }

        await _context.SaveChangesAsync();
        _pageCache.InvalidateUser(userId);
        StatusMessage = AddressInput.UserAddressId.HasValue ? "Dirección actualizada." : "Dirección agregada.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetDefaultAsync(long id)
    {
        var userId = _userManager.GetUserId(User);
        var address = await _context.Set<UserAddress>().SingleOrDefaultAsync(item => item.UserAddressId == id && item.IdentityUserId == userId);
        if (address is null) return NotFound();
        await _context.Set<UserAddress>().Where(item => item.IdentityUserId == userId).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false));
        address.IsDefault = true;
        address.UpdatedAt = DateTimeOffset.UtcNow;
        await SyncProfileAddressAsync(userId, address);
        await _context.SaveChangesAsync();
        _pageCache.InvalidateUser(userId);
        StatusMessage = $"{address.Label} ahora es tu dirección principal.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAddressAsync(long id)
    {
        var userId = _userManager.GetUserId(User);
        var address = await _context.Set<UserAddress>().SingleOrDefaultAsync(item => item.UserAddressId == id && item.IdentityUserId == userId);
        if (address is null) return NotFound();
        if (address.IsDefault)
        {
            StatusMessage = "Error: elige otra dirección principal antes de eliminar esta.";
            return RedirectToPage();
        }
        _context.Set<UserAddress>().Remove(address);
        await _context.SaveChangesAsync();
        _pageCache.InvalidateUser(userId);
        StatusMessage = "Dirección eliminada.";
        return RedirectToPage();
    }

    private async Task LoadAsync(string userId)
    {
        Provinces = await _pageCache.GetProvincesAsync(_context, HttpContext.RequestAborted);
        Cities = await _pageCache.GetCitiesAsync(_context, HttpContext.RequestAborted);
        Addresses = await _context.Set<UserAddress>().AsNoTracking().Where(item => item.IdentityUserId == userId)
            .Include(item => item.Province).Include(item => item.City)
            .OrderByDescending(item => item.IsDefault).ThenBy(item => item.Label).ToListAsync();
    }

    private async Task EnsurePrimaryAddressAsync(string userId)
    {
        if (await _context.Set<UserAddress>().AnyAsync(item => item.IdentityUserId == userId)) return;
        var profile = await _context.Set<UserProfile>().AsNoTracking().SingleOrDefaultAsync(item => item.IdentityUserId == userId);
        if (profile is null || string.IsNullOrWhiteSpace(profile.AddressLine1)) return;
        _context.Set<UserAddress>().Add(new UserAddress { IdentityUserId = userId, Label = "Casa", AddressLine1 = profile.AddressLine1, AddressLine2 = profile.AddressLine2, ProvinceCode = profile.ProvinceCode, CityCode = profile.CityCode, Reference = profile.Reference, IsDefault = true });
        await _context.SaveChangesAsync();
        _pageCache.InvalidateUser(userId);
    }

    private async Task SyncProfileAddressAsync(string userId, UserAddress address)
    {
        var profile = await _context.Set<UserProfile>().SingleOrDefaultAsync(item => item.IdentityUserId == userId);
        if (profile is null) return;
        profile.AddressLine1 = address.AddressLine1; profile.AddressLine2 = address.AddressLine2;
        profile.ProvinceCode = address.ProvinceCode; profile.CityCode = address.CityCode; profile.Reference = address.Reference;
    }
}
