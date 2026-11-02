using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Controllers;
using OrbiApp.Presentation.Client.Networking;
using Xunit;

namespace OrbiApp.Contract.Tests.Delivery;

public sealed class DeliveryOrdersApiContractTests
{
    [Fact]
    public void Orders_and_deliveries_keep_their_client_routes()
    {
        Assert.Contains("/Delivery/MyOrders", Routes<OrbiApp.Presentation.Client.Pages.Delivery.MyOrders>());
        Assert.Contains("/Delivery/Deliveries", Routes<OrbiApp.Presentation.Client.Pages.Delivery.Deliveries>());
    }

    [Fact]
    public void Api_limits_customer_and_delivery_queries_to_their_roles()
    {
        Assert.Equal("Usuario", Assert.Single(Method<AuthorizeAttribute>(nameof(DeliveryOrdersApiController.GetCustomerOrders))).Roles);
        Assert.Equal("Repartidor", Assert.Single(Method<AuthorizeAttribute>(nameof(DeliveryOrdersApiController.GetAssignedDeliveries))).Roles);
        Assert.Equal("Repartidor", Assert.Single(Method<AuthorizeAttribute>(nameof(DeliveryOrdersApiController.UpdateDeliveryStatus))).Roles);
        Assert.Equal("api/delivery", Assert.Single(typeof(DeliveryOrdersApiController).GetCustomAttributes<RouteAttribute>()).Template);
    }

    [Fact]
    public void Delivery_status_mutation_requires_antiforgery_and_uses_put()
    {
        var method = typeof(DeliveryOrdersApiController).GetMethod(nameof(DeliveryOrdersApiController.UpdateDeliveryStatus));
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal("deliveries/{orderId:int}/status", Assert.Single(method.GetCustomAttributes<HttpPutAttribute>()).Template);
    }

    [Fact]
    public void Delivery_status_request_rejects_empty_or_overlong_values()
    {
        Assert.False(IsValid(new UpdateDeliveryOrderStatusRequest("")));
        Assert.False(IsValid(new UpdateDeliveryOrderStatusRequest(new string('x', 31))));
        Assert.True(IsValid(new UpdateDeliveryOrderStatusRequest("En camino")));
    }

    [Fact]
    public void Old_mvc_order_and_delivery_listing_actions_are_removed()
    {
        Assert.Null(typeof(DeliveryController).GetMethod("MyOrders"));
        Assert.Null(typeof(DeliveryController).GetMethod("Deliveries"));
    }

    private static IEnumerable<string?> Routes<T>() =>
        typeof(T).GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>().Select(attribute => attribute.Template);

    private static IEnumerable<T> Method<T>(string name) where T : Attribute =>
        typeof(DeliveryOrdersApiController).GetMethod(name)!.GetCustomAttributes<T>();

    private static bool IsValid(object value) =>
        Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), validateAllProperties: true);
}
