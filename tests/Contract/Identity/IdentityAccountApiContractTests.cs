using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Controllers;
using Microsoft.AspNetCore.Http.Timeouts;
using OrbiApp.Presentation.Client.Networking;
using OrbiApp.Presentation.Client.Pages.Identity.Manage;
using OrbiApp.Presentation.Client.Pages.Identity;
using Xunit;

namespace OrbiApp.Contract.Tests.Identity;

public sealed class IdentityAccountApiContractTests
{
    [Fact]
    public void Profile_and_address_pages_keep_their_identity_routes()
    {
        Assert.Contains("/Identity/Account/Manage", Routes<Profile>());
        Assert.Contains("/Identity/Account/Manage/Index", Routes<Profile>());
        Assert.Contains("/Identity/Account/Manage/Addresses", Routes<Addresses>());
        Assert.Contains("/Identity/Account/Manage/PaymentMethods", Routes<PaymentMethods>());
        Assert.Contains("/Identity/Account/Login", Routes<Login>());
    }

    [Fact]
    public void Account_and_address_apis_require_an_authenticated_user()
    {
        Assert.NotNull(typeof(IdentityAccountApiController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(typeof(IdentityAddressesApiController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/identity/account", Assert.Single(typeof(IdentityAccountApiController).GetCustomAttributes<RouteAttribute>()).Template);
        Assert.Equal("api/identity/account/addresses", Assert.Single(typeof(IdentityAddressesApiController).GetCustomAttributes<RouteAttribute>()).Template);
        Assert.NotNull(typeof(IdentityAuthenticationApiController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("api/identity/auth", Assert.Single(typeof(IdentityAuthenticationApiController).GetCustomAttributes<RouteAttribute>()).Template);
    }

    [Fact]
    public void Registration_options_api_remains_public_and_keeps_its_timeout_policy()
    {
        var controller = typeof(IdentityRegistrationOptionsApiController);
        var action = controller.GetMethod(nameof(IdentityRegistrationOptionsApiController.Get));

        Assert.NotNull(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("api/identity/registration", Assert.Single(controller.GetCustomAttributes<RouteAttribute>()).Template);
        Assert.Equal("options", Assert.Single(action!.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Equal("api", Assert.Single(action!.GetCustomAttributes<RequestTimeoutAttribute>()).PolicyName);
    }

    [Fact]
    public void Password_recovery_routes_and_api_mutations_are_available_to_anonymous_users()
    {
        Assert.Contains("/Identity/Account/ForgotPassword", Routes<ForgotPassword>());
        Assert.Contains("/Identity/Account/ResetPassword", Routes<ResetPassword>());

        AssertMutation<IdentityAuthenticationApiController>(nameof(IdentityAuthenticationApiController.ForgotPassword), typeof(HttpPostAttribute));
        AssertMutation<IdentityAuthenticationApiController>(nameof(IdentityAuthenticationApiController.ResetPassword), typeof(HttpPostAttribute));

        Assert.Equal("forgot-password", typeof(IdentityAuthenticationApiController)
            .GetMethod(nameof(IdentityAuthenticationApiController.ForgotPassword))!
            .GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal("reset-password", typeof(IdentityAuthenticationApiController)
            .GetMethod(nameof(IdentityAuthenticationApiController.ResetPassword))!
            .GetCustomAttribute<HttpPostAttribute>()!.Template);
    }

    [Fact]
    public void Password_recovery_requests_keep_validation_rules()
    {
        Assert.False(IsValid(new ForgotPasswordRequest("correo-invalido")));
        Assert.True(IsValid(new ForgotPasswordRequest("persona@example.com")));
        Assert.False(IsValid(new ResetPasswordRequest("persona@example.com", "123", "123", "codigo")));
        Assert.False(IsValid(new ResetPasswordRequest("persona@example.com", "secreto123", "distinta", "codigo")));
        Assert.True(IsValid(new ResetPasswordRequest("persona@example.com", "secreto123", "secreto123", "codigo")));
    }

    [Fact]
    public void Account_and_address_mutations_require_antiforgery()
    {
        AssertMutation<IdentityAccountApiController>(nameof(IdentityAccountApiController.UpdatePhone), typeof(HttpPutAttribute));
        AssertMutation<IdentityAccountApiController>(nameof(IdentityAccountApiController.ChangeEmail), typeof(HttpPostAttribute));
        AssertMutation<IdentityAccountApiController>(nameof(IdentityAccountApiController.SendVerificationEmail), typeof(HttpPostAttribute));
        AssertMutation<IdentityAccountApiController>(nameof(IdentityAccountApiController.Logout), typeof(HttpPostAttribute));
        AssertMutation<IdentityAuthenticationApiController>(nameof(IdentityAuthenticationApiController.Login), typeof(HttpPostAttribute));
        AssertMutation<IdentityAddressesApiController>(nameof(IdentityAddressesApiController.Save), typeof(HttpPutAttribute));
        AssertMutation<IdentityAddressesApiController>(nameof(IdentityAddressesApiController.SetDefault), typeof(HttpPutAttribute));
        AssertMutation<IdentityAddressesApiController>(nameof(IdentityAddressesApiController.Delete), typeof(HttpDeleteAttribute));
    }

    [Fact]
    public void Address_input_enforces_required_fields_and_maximum_lengths()
    {
        Assert.False(IsValid(new SaveIdentityAddressRequest()));
        Assert.False(IsValid(new SaveIdentityAddressRequest
        {
            Label = new string('x', 41),
            AddressLine1 = new string('x', 161),
            ProvinceCode = "P",
            CityCode = "C"
        }));
        Assert.True(IsValid(new SaveIdentityAddressRequest
        {
            Label = "Casa",
            AddressLine1 = "Av. Principal",
            ProvinceCode = "P",
            CityCode = "C"
        }));
    }

    [Fact]
    public void Phone_and_email_inputs_keep_identity_validation()
    {
        Assert.False(IsValid(new ChangeIdentityEmailRequest { NewEmail = "no-es-correo" }));
        Assert.True(IsValid(new ChangeIdentityEmailRequest { NewEmail = "persona@example.com" }));
        Assert.False(IsValid(new UpdateIdentityPhoneRequest { PhoneNumber = "not a phone" }));
    }

    private static IEnumerable<string?> Routes<T>() =>
        typeof(T).GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>().Select(attribute => attribute.Template);

    private static void AssertMutation<TController>(string methodName, Type httpVerbAttribute)
    {
        var method = typeof(TController).GetMethod(methodName);
        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(method.GetCustomAttributes().SingleOrDefault(attribute => httpVerbAttribute.IsInstanceOfType(attribute)));
    }

    private static bool IsValid(object value) =>
        Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), validateAllProperties: true);
}
