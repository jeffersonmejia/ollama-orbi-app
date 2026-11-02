using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using OrbiApp.Presentation.Client.Pages.Home;
using Xunit;

namespace OrbiApp.Contract.Tests.Home;

public sealed class PublicHomeContractTests
{
    [Fact]
    public void Public_home_keeps_root_route_and_allows_anonymous_visitors()
    {
        Assert.Contains("/", typeof(PublicHome).GetCustomAttributes<RouteAttribute>().Select(route => route.Template));
        Assert.NotNull(typeof(PublicHome).GetCustomAttributes<AllowAnonymousAttribute>().SingleOrDefault());
    }
}
