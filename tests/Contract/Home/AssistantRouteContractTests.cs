using System.Reflection;
using Microsoft.AspNetCore.Components;
using OrbiApp.Controllers;
using OrbiApp.Presentation.Client.Pages;
using Xunit;

namespace OrbiApp.Contract.Tests.Home;

public sealed class AssistantRouteContractTests
{
    [Fact]
    public void Disabled_assistant_preserves_legacy_and_client_routes()
    {
        var routes = typeof(Assistant).GetCustomAttributes<RouteAttribute>()
            .Select(route => route.Template);

        Assert.Contains("/Home/Assistant", routes);
        Assert.Contains("/app/asistente", routes);
        Assert.Null(typeof(HomeController).GetMethod("Assistant"));
    }
}
