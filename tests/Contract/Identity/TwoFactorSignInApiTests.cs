using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OrbiApp.Contract.Tests.Identity.Support;
using OrbiApp.Controllers;
using OrbiApp.Presentation.Client.Pages.Identity;
using Xunit;

namespace OrbiApp.Contract.Tests.Identity;

public sealed class TwoFactorSignInApiTests
{
    [Fact]
    public void Two_factor_pages_and_api_keep_the_existing_routes_and_antiforgery()
    {
        var routes = typeof(TwoFactorLogin).GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>().Select(route => route.Template);
        Assert.Contains("/Identity/Account/LoginWith2fa", routes);
        Assert.Contains("/Identity/Account/LoginWithRecoveryCode", routes);
        Assert.NotNull(typeof(IdentityTwoFactorSignInApiController).GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var name in new[] { nameof(IdentityTwoFactorSignInApiController.Authenticator), nameof(IdentityTwoFactorSignInApiController.RecoveryCode) })
            Assert.NotNull(typeof(IdentityTwoFactorSignInApiController).GetMethod(name)!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task No_two_factor_challenge_cannot_submit_a_verification_code()
    {
        using var fixture = new PasswordIdentityFixture();
        fixture.SignIn.TwoFactorResult = Microsoft.AspNetCore.Identity.SignInResult.Success;
        var controller = CreateController(fixture);
        Assert.IsType<UnauthorizedObjectResult>(await controller.Authenticator(new() { TwoFactorCode = "123456" }));
        Assert.IsType<UnauthorizedObjectResult>(await controller.RecoveryCode(new() { RecoveryCode = "recovery-code" }));
        Assert.Null(fixture.SignIn.SubmittedCode);
    }

    [Fact]
    public async Task Authenticator_normalizes_code_and_preserves_remember_options()
    {
        using var fixture = new PasswordIdentityFixture();
        fixture.SignIn.TwoFactorUser = fixture.User;
        fixture.SignIn.TwoFactorResult = Microsoft.AspNetCore.Identity.SignInResult.Success;
        var result = await CreateController(fixture).Authenticator(new() { TwoFactorCode = "12-3456", RememberMe = true, RememberMachine = true });
        Assert.IsType<NoContentResult>(result);
        Assert.Equal("123456", fixture.SignIn.SubmittedCode);
        Assert.True(fixture.SignIn.RememberMe);
        Assert.True(fixture.SignIn.RememberMachine);
    }

    [Fact]
    public async Task Recovery_sign_in_normalizes_spaces_and_preserves_failure_and_lockout_states()
    {
        using var fixture = new PasswordIdentityFixture();
        fixture.SignIn.TwoFactorUser = fixture.User;
        var controller = CreateController(fixture);
        Assert.IsType<UnauthorizedObjectResult>(await controller.RecoveryCode(new() { RecoveryCode = "ab cd-ef" }));
        Assert.Equal("abcd-ef", fixture.SignIn.SubmittedCode);
        fixture.SignIn.TwoFactorResult = Microsoft.AspNetCore.Identity.SignInResult.LockedOut;
        Assert.Equal(423, Assert.IsType<ObjectResult>(await controller.RecoveryCode(new() { RecoveryCode = "code" })).StatusCode);
    }

    private static IdentityTwoFactorSignInApiController CreateController(PasswordIdentityFixture fixture) =>
        new(fixture.SignIn, NullLogger<IdentityTwoFactorSignInApiController>.Instance);
}
