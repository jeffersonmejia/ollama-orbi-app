using System.ComponentModel.DataAnnotations;

namespace OrbiApp.Models.Delivery;

public sealed class CatalogAddressInput
{
    public long? UserAddressId { get; set; }

    [Required(ErrorMessage = "Escribe un nombre para la dirección.")]
    [StringLength(40)]
    public string Label { get; set; } = "Casa";

    [Required(ErrorMessage = "La calle principal es obligatoria.")]
    [StringLength(160)]
    public string AddressLine1 { get; set; } = string.Empty;

    [Required(ErrorMessage = "La calle secundaria es obligatoria.")]
    [StringLength(160)]
    public string AddressLine2 { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona una provincia.")]
    public string ProvinceCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona una ciudad.")]
    public string CityCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe una referencia para ubicarte.")]
    [StringLength(240)]
    public string Reference { get; set; } = string.Empty;
}
