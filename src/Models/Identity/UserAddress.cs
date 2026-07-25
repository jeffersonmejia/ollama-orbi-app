namespace SakilaApp.Models.Identity;

public class UserAddress
{
    public long UserAddressId { get; set; }
    public string IdentityUserId { get; set; } = string.Empty;
    public string Label { get; set; } = "Casa";
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string ProvinceCode { get; set; } = string.Empty;
    public string CityCode { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public UserProfile Profile { get; set; } = null!;
    public EcuadorProvince Province { get; set; } = null!;
    public EcuadorCity City { get; set; } = null!;

    public string FormattedAddress => string.Join(", ", new[]
    {
        AddressLine1,
        AddressLine2,
        City?.Name,
        Province?.Name,
        string.IsNullOrWhiteSpace(Reference) ? null : $"Ref. {Reference}"
    }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
