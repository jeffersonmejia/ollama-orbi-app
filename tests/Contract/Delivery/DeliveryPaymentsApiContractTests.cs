using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Controllers;
using Xunit;

namespace OrbiApp.Contract.Tests.Delivery;

public sealed class DeliveryPaymentsApiContractTests
{
    [Fact]
    public void Payments_api_and_client_route_require_customer_role()
    {
        var controllerRoute = Assert.Single(typeof(DeliveryPaymentsApiController).GetCustomAttributes<RouteAttribute>());
        var action = typeof(DeliveryPaymentsApiController).GetMethod(nameof(DeliveryPaymentsApiController.Get));
        Assert.NotNull(action);

        Assert.Equal("api/delivery/payments", controllerRoute.Template);
        Assert.Equal("Usuario", Assert.Single(typeof(DeliveryPaymentsApiController).GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.NotNull(action!.GetCustomAttribute<HttpGetAttribute>());
        Assert.Contains("/Delivery/MyPayments", typeof(OrbiApp.Presentation.Client.Pages.Delivery.MyPayments)
            .GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>().Select(route => route.Template));
    }

    [Fact]
    public void Customer_payment_list_no_longer_uses_the_mvc_action()
    {
        Assert.Null(typeof(DeliveryController).GetMethod("MyPayments"));
    }
}
