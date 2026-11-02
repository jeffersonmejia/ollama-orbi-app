using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Controllers;
using OrbiApp.Presentation.Client.Networking;
using Xunit;

namespace OrbiApp.Contract.Tests.Delivery;

public sealed class DeliveryCartApiContractTests
{
    [Fact]
    public void Cart_component_keeps_the_existing_route()
    {
        var routes = typeof(OrbiApp.Presentation.Client.Pages.Delivery.Cart)
            .GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(attribute => attribute.Template);

        Assert.Contains("/Delivery/Cart", routes);
    }

    [Fact]
    public void Cart_api_requires_customer_role()
    {
        var authorization = Assert.Single(typeof(DeliveryCartApiController).GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal("Usuario", authorization.Roles);
        Assert.Equal("api/delivery/cart", Assert.Single(typeof(DeliveryCartApiController).GetCustomAttributes<RouteAttribute>()).Template);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(999, true)]
    [InlineData(0, false)]
    [InlineData(1000, false)]
    public void Cart_quantity_contract_enforces_supported_range(int quantity, bool isValid)
    {
        var request = new UpdateDeliveryCartQuantityRequest(quantity);
        var results = new List<ValidationResult>();
        var actual = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.Equal(isValid, actual);
    }

    [Fact]
    public void Cart_mutations_use_antiforgery_validation()
    {
        AssertMutationIsProtected(nameof(DeliveryCartApiController.UpdateQuantity));
        AssertMutationIsProtected(nameof(DeliveryCartApiController.Remove));
    }

    [Fact]
    public void Checkout_exposes_a_post_api_route_and_keeps_antiforgery_validation()
    {
        var action = typeof(DeliveryController).GetMethod(nameof(DeliveryController.Checkout));
        Assert.NotNull(action);

        Assert.Contains(action!.GetCustomAttributes<HttpPostAttribute>(), attribute => attribute.Template == "/api/delivery/checkout");
        Assert.NotNull(action.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    private static void AssertMutationIsProtected(string methodName)
    {
        var method = typeof(DeliveryCartApiController).GetMethod(methodName);
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
