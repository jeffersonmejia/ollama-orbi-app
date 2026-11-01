namespace OrbiApp.Presentation.Client;

public sealed record RegistrationOptionsResponse(
    string RequestToken,
    IReadOnlyList<string> Roles,
    IReadOnlyList<RegistrationProvinceOption> Provinces,
    IReadOnlyList<RegistrationCityOption> Cities);

public sealed record RegistrationProvinceOption(string Code, string Name);

public sealed record RegistrationCityOption(string Code, string Name, string ProvinceCode);
