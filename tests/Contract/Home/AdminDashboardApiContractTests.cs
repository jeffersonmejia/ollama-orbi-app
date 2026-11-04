using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Controllers;
using OrbiApp.Presentation.Client.Pages.Home;
using Xunit;

namespace OrbiApp.Contract.Tests.Home;

public sealed class AdminDashboardApiContractTests
{
    [Fact]
    public void Admin_dashboard_uses_a_role_protected_api_and_preserves_its_route_in_wasm()
    {
        var controller = typeof(HomeDashboardApiController);
        var page = typeof(AdminDashboard);

        Assert.Equal("api/home", Assert.Single(controller.GetCustomAttributes<RouteAttribute>()).Template);
        Assert.Equal("Administrador", Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>()).Roles);

        var action = Assert.Single(controller.GetMethods(), method => method.Name == nameof(HomeDashboardApiController.GetAdminMetrics));
        Assert.Equal("admin-metrics", Assert.Single(action.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Contains("/Home/PanelAdministrador", page.GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(route => route.Template));
        Assert.Equal("Administrador", Assert.Single(page.GetCustomAttributes<AuthorizeAttribute>()).Roles);
    }

    [Fact]
    public void Admin_dashboard_no_longer_exposes_the_mvc_action()
    {
        Assert.Null(typeof(HomeController).GetMethod("PanelAdministrador"));
    }
}
