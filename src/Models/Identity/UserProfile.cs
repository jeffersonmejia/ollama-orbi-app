namespace SakilaApp.Models.Identity;

public class UserProfile
{
    public string IdentityUserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string ProvinceCode { get; set; } = string.Empty;
    public string CityCode { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public EcuadorProvince Province { get; set; } = null!;
    public EcuadorCity City { get; set; } = null!;
}
