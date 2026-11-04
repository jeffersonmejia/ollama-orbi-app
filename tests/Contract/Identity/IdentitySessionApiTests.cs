using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Controllers;
using OrbiApp.Features.Identity.Client.Contracts;
using Xunit;

namespace OrbiApp.Contract.Tests.Identity;

public sealed class IdentitySessionApiTests
{
    [Fact]
    public void Session_snapshot_is_not_http_cacheable()
    {
        Assert.True(typeof(IdentitySessionApiController).GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        Assert.NotNull(typeof(IdentitySessionApiController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("api/identity/auth/session", typeof(IdentitySessionApiController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public void Anonymous_snapshot_contains_no_identity_information()
    {
        var result = ReadSession(new ClaimsIdentity([new Claim(ClaimTypes.Name, "untrusted")]));
        Assert.False(result.IsAuthenticated);
        Assert.Null(result.Name);
        Assert.Null(result.UserId);
        Assert.Empty(result.Roles);
    }

    [Fact]
    public void Snapshot_projects_only_ui_identity_fields_from_the_authenticated_cookie()
    {
        var result = ReadSession(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Name, "test@example.invalid"),
            new Claim(ClaimTypes.Email, "test@example.invalid"), new Claim(ClaimTypes.GivenName, "Test"),
            new Claim(ClaimTypes.Role, "Usuario"), new Claim("security-stamp", "sensitive-value")], "cookie"));
        Assert.True(result.IsAuthenticated);
        Assert.Equal("user-1", result.UserId);
        Assert.Equal("Test", result.GivenName);
        Assert.Equal(["Usuario"], result.Roles);
        Assert.DoesNotContain("sensitive-value", System.Text.Json.JsonSerializer.Serialize(result));
    }

    private static IdentitySessionResponse ReadSession(ClaimsIdentity identity)
    {
        var controller = new IdentitySessionApiController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
        return Assert.IsType<IdentitySessionResponse>(Assert.IsType<OkObjectResult>(controller.Get().Result).Value);
    }
}
