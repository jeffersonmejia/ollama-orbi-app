using System.ComponentModel.DataAnnotations;

namespace OrbiApp.Presentation.Client.Networking;

public sealed record IdentityProfileResponse(
    string Username,
    string Email,
    bool IsEmailConfirmed,
    string RoleSummary,
    string? PhoneNumber);

public sealed class UpdateIdentityPhoneRequest
{
    [Phone(ErrorMessage = "Ingresa un número de teléfono válido.")]
    public string? PhoneNumber { get; set; }
}

public sealed class ChangeIdentityEmailRequest
{
    [Required(ErrorMessage = "Ingresa el nuevo correo.")]
    [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
    public string NewEmail { get; set; } = string.Empty;
}

public sealed class SaveIdentityAddressRequest
{
    public long? UserAddressId { get; set; }

    [Required(ErrorMessage = "Escribe un nombre breve.")]
    [StringLength(40)]
    public string Label { get; set; } = "Casa";

    [Required(ErrorMessage = "La calle principal es obligatoria.")]
    [StringLength(160)]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(160)]
    public string AddressLine2 { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona una provincia.")]
    public string ProvinceCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona una ciudad.")]
    public string CityCode { get; set; } = string.Empty;

    [StringLength(240)]
    public string? Reference { get; set; }

    public bool IsDefault { get; set; }
}

public sealed record IdentityAddressBookResponse(
    IReadOnlyList<IdentityAddressSummary> Addresses,
    IReadOnlyList<IdentityProvinceOption> Provinces,
    IReadOnlyList<IdentityCityOption> Cities);

public sealed record IdentityAddressSummary(
    long UserAddressId,
    string Label,
    string AddressLine1,
    string AddressLine2,
    string ProvinceCode,
    string ProvinceName,
    string CityCode,
    string CityName,
    string? Reference,
    bool IsDefault,
    string FormattedAddress);

public sealed record IdentityProvinceOption(string Code, string Name);

public sealed record IdentityCityOption(string Code, string Name, string ProvinceCode);

public sealed record IdentityAccountActionResponse(string Message);
