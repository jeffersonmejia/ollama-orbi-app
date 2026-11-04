using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using OrbiApp.Presentation.Client.Pages.Identity;
using Xunit;

namespace OrbiApp.Contract.Tests.Identity;

public sealed class IdentityStatusRouteContractTests
{
    [Fact]
    public void Identity_status_pages_keep_their_urls_and_are_anonymous()
    {
        var routes = typeof(IdentityStatus).GetCustomAttributes<RouteAttribute>()
            .Select(route => route.Template)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("/Identity/Account/AccessDenied", routes);
        Assert.Contains("/Identity/Account/Lockout", routes);
        Assert.Contains("/Identity/Account/ForgotPasswordConfirmation", routes);
        Assert.Contains("/Identity/Account/ResetPasswordConfirmation", routes);
        Assert.NotNull(typeof(IdentityStatus).GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
