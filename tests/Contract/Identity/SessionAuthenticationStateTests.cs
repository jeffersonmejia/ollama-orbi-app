using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrbiApp.Contract.Tests.Identity.Support;
using OrbiApp.Features.Identity.Client.Authentication;
using OrbiApp.Features.Identity.Client.Contracts;
using OrbiApp.Presentation.Client;
using Xunit;

namespace OrbiApp.Contract.Tests.Identity;

public sealed class SessionAuthenticationStateTests
{
    [Fact]
    public async Task Initial_authentication_reuses_the_persisted_snapshot_without_an_http_request()
    {
        using var handler = new SessionHttpHandler();
        using var api = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Usuario")], "cookie"));
        var provider = new SessionAuthenticationStateProvider(new SnapshotAuthenticationStateProvider(principal), api);
        Assert.Same(principal, (await provider.GetAuthenticationStateAsync()).User);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Login_refresh_updates_roles_name_and_subscribers_without_recreating_the_provider()
    {
        using var handler = new SessionHttpHandler();
        using var api = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var provider = new SessionAuthenticationStateProvider(new SnapshotAuthenticationStateProvider(new(new ClaimsIdentity())), api);
        Task<AuthenticationState>? notification = null;
        provider.AuthenticationStateChanged += state => notification = state;
        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        var refreshed = await provider.RefreshAsync();
        Assert.True(refreshed.User.Identity!.IsAuthenticated);
        Assert.Equal("test@example.invalid", refreshed.User.Identity.Name);
        Assert.True(refreshed.User.IsInRole("Usuario"));
        Assert.Same(refreshed, await notification!);
        Assert.Same(refreshed, await provider.GetAuthenticationStateAsync());
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Logout_wins_over_an_older_session_refresh_in_flight()
    {
        var completion = new TaskCompletionSource<IdentitySessionResponse>();
        using var handler = new SessionHttpHandler { Response = () => completion.Task };
        using var api = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var provider = new SessionAuthenticationStateProvider(new SnapshotAuthenticationStateProvider(new(new ClaimsIdentity())), api);
        var pending = provider.RefreshAsync();
        provider.SetSignedOut();
        completion.SetResult(new(true, "old-user", "old", null, null, ["Usuario"]));
        Assert.False((await pending).User.Identity!.IsAuthenticated);
        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task Client_registration_preserves_and_wraps_the_existing_snapshot_provider()
    {
        var services = new ServiceCollection();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Usuario")], "cookie"));
        services.AddScoped<AuthenticationStateProvider>(_ => new SnapshotAuthenticationStateProvider(principal));
        services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("https://example.invalid/") });
        services.AddIdentityClientAuthentication();
        using var container = services.BuildServiceProvider();
        using var scope = container.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        Assert.IsType<SessionAuthenticationStateProvider>(provider);
        Assert.Same(principal, (await provider.GetAuthenticationStateAsync()).User);
    }

    [Theory]
    [InlineData("/Delivery?search=test", true)]
    [InlineData("/Identity/Account/Manage/ChangePassword", true)]
    [InlineData("/Identity/Account/Login", true)]
    [InlineData("/", true)]
    [InlineData("/Delivery/Admin", false)]
    [InlineData("https://other.invalid/Delivery", false)]
    [InlineData("//other.invalid/Delivery", false)]
    public void Client_route_detection_keeps_migrated_destinations_inside_the_router(string target, bool expected) =>
        Assert.Equal(expected, ClientRouteRegistry.Contains(target, "https://example.invalid/"));
}
