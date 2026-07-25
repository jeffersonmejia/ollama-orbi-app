namespace SakilaApp.Models.Identity;

public class EcuadorCity
{
    public string Code { get; set; } = string.Empty;
    public string ProvinceCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public EcuadorProvince Province { get; set; } = null!;
}
