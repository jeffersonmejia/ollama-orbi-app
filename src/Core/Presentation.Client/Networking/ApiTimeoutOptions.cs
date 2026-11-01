namespace OrbiApp.Presentation.Client.Networking;

public sealed class ApiTimeoutOptions
{
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int HttpClientTimeoutSeconds { get; set; } = 35;
}
