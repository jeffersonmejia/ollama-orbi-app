namespace OrbiApp.Features.Identity.Client.Contracts;

public sealed record IdentitySessionResponse(
    bool IsAuthenticated,
    string? UserId,
    string? Name,
    string? Email,
    string? GivenName,
    IReadOnlyList<string> Roles);
