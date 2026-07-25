namespace SakilaApp.Models.Identity;

public class EcuadorProvince
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ICollection<EcuadorCity> Cities { get; set; } = new List<EcuadorCity>();
}
