using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using OrbiApp.Features.Identity.Client.Contracts;

namespace OrbiApp.Features.Identity.Client.Authentication;

public sealed class SessionAuthenticationStateProvider(AuthenticationStateProvider initialState, HttpClient api)
    : AuthenticationStateProvider
{
    private Task<AuthenticationState>? _state;
    private int _version;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        _state ??= initialState.GetAuthenticationStateAsync();

    public async Task<AuthenticationState> RefreshAsync()
    {
        var version = ++_version;
        var session = await api.GetFromJsonAsync<IdentitySessionResponse>("api/identity/auth/session")
            ?? throw new JsonException("No se pudo consultar la sesión.");
        if (version != _version) return await GetAuthenticationStateAsync();

        var claims = new List<Claim>();
        if (session.IsAuthenticated)
        {
            AddClaim(claims, ClaimTypes.NameIdentifier, session.UserId);
            AddClaim(claims, ClaimTypes.Name, session.Name);
            AddClaim(claims, ClaimTypes.Email, session.Email);
            AddClaim(claims, ClaimTypes.GivenName, session.GivenName);
            claims.AddRange(session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        }
        var identity = session.IsAuthenticated ? new ClaimsIdentity(claims, "Identity.Application") : new ClaimsIdentity();
        return Update(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    public void SetSignedOut()
    {
        ++_version;
        Update(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private AuthenticationState Update(AuthenticationState state)
    {
        _state = Task.FromResult(state);
        NotifyAuthenticationStateChanged(_state);
        return state;
    }

    private static void AddClaim(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrEmpty(value)) claims.Add(new Claim(type, value));
    }
}
