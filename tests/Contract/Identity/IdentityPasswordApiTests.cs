using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Contract.Tests.Identity.Support;
using OrbiApp.Controllers;
using OrbiApp.Features.Identity.Client.Contracts;
using OrbiApp.Presentation.Client.Pages.Identity.Manage;
using Xunit;

namespace OrbiApp.Contract.Tests.Identity;

public sealed class IdentityPasswordApiTests
{
    [Fact]
    public void Password_routes_require_authentication_and_mutations_require_antiforgery()
    {
        var controller = typeof(IdentityPasswordApiController);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/identity/account/password", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.True(controller.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        Assert.NotNull(controller.GetMethod(nameof(IdentityPasswordApiController.Change))!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(controller.GetMethod(nameof(IdentityPasswordApiController.Set))!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(typeof(Password).GetCustomAttribute<AuthorizeAttribute>());
        var routes = typeof(Password).GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>().Select(route => route.Template);
        Assert.Contains("/Identity/Account/Manage/ChangePassword", routes);
        Assert.Contains("/Identity/Account/Manage/SetPassword", routes);
    }

    [Fact]
    public async Task Incorrect_current_password_does_not_change_credentials_or_refresh_session()
    {
        using var fixture = new PasswordIdentityFixture();
        var response = await fixture.Controller.Change(ChangeRequest("Wrong1!", "Replacement2!"));
        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.True(await fixture.Users.CheckPasswordAsync(fixture.User, "Current1!"));
        Assert.Equal(0, fixture.SignIn.RefreshCount);
    }

    [Fact]
    public async Task Change_replaces_password_and_refreshes_the_current_session()
    {
        using var fixture = new PasswordIdentityFixture();
        var response = await fixture.Controller.Change(ChangeRequest("Current1!", "Replacement2!"));
        Assert.IsType<OkObjectResult>(response.Result);
        Assert.False(await fixture.Users.CheckPasswordAsync(fixture.User, "Current1!"));
        Assert.True(await fixture.Users.CheckPasswordAsync(fixture.User, "Replacement2!"));
        Assert.Equal(1, fixture.SignIn.RefreshCount);
    }

    [Fact]
    public async Task Set_does_not_overwrite_an_existing_password_without_current_password()
    {
        using var fixture = new PasswordIdentityFixture();
        var response = await fixture.Controller.Set(new() { NewPassword = "Replacement2!", ConfirmPassword = "Replacement2!" });
        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.True(await fixture.Users.CheckPasswordAsync(fixture.User, "Current1!"));
        Assert.Equal(0, fixture.SignIn.RefreshCount);
    }

    [Fact]
    public async Task Set_adds_a_password_to_an_external_account_and_refreshes_session()
    {
        using var fixture = new PasswordIdentityFixture(hasPassword: false);
        var status = await fixture.Controller.Get();
        Assert.False(Assert.IsType<IdentityPasswordStatusResponse>(Assert.IsType<OkObjectResult>(status.Result).Value).HasPassword);
        var response = await fixture.Controller.Set(new() { NewPassword = "Replacement2!", ConfirmPassword = "Replacement2!" });
        Assert.IsType<OkObjectResult>(response.Result);
        Assert.True(await fixture.Users.CheckPasswordAsync(fixture.User, "Replacement2!"));
        Assert.Equal(1, fixture.SignIn.RefreshCount);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("nouppercase1!")]
    [InlineData("NoDigitHere!")]
    public async Task Identity_password_policy_is_enforced_on_the_server(string password)
    {
        using var fixture = new PasswordIdentityFixture(hasPassword: false);
        var response = await fixture.Controller.Set(new() { NewPassword = password, ConfirmPassword = password });
        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(fixture.User.PasswordHash);
        Assert.Equal(0, fixture.SignIn.RefreshCount);
    }

    [Fact]
    public void Request_validation_rejects_missing_current_password_and_mismatched_confirmation()
    {
        var request = ChangeRequest(string.Empty, "Replacement2!");
        request.ConfirmPassword = "Other3!";
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(request.OldPassword)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(request.ConfirmPassword)));
    }

    private static ChangeIdentityPasswordRequest ChangeRequest(string oldPassword, string newPassword) =>
        new() { OldPassword = oldPassword, NewPassword = newPassword, ConfirmPassword = newPassword };
}
