using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using OrbiApp.Features.Identity.Client.Contracts;

namespace OrbiApp.Contract.Tests.Identity.Support;

internal sealed class SessionHttpHandler : HttpMessageHandler
{
    public int RequestCount { get; private set; }
    public Func<Task<IdentitySessionResponse>> Response { get; set; } = () =>
        Task.FromResult(new IdentitySessionResponse(true, "user-1", "test@example.invalid", "test@example.invalid", "Test", ["Usuario"]));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        if (request.RequestUri?.AbsolutePath != "/api/identity/auth/session") throw new InvalidOperationException("Unexpected endpoint.");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(await Response()) };
    }
}

internal sealed class SnapshotAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
}
