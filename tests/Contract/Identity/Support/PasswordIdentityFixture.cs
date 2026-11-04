using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrbiApp.Controllers;

namespace OrbiApp.Contract.Tests.Identity.Support;

internal sealed class PasswordIdentityFixture : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    public IdentityUser User { get; } = new() { Id = "test-user", UserName = "test@example.invalid" };
    public UserManager<IdentityUser> Users { get; }
    public RecordingSignInManager SignIn { get; }
    public IdentityPasswordApiController Controller { get; }

    public PasswordIdentityFixture(bool hasPassword = true)
    {
        var store = new PasswordUserStore(User);
        var options = Options.Create(new IdentityOptions());
        Users = new UserManager<IdentityUser>(store, options, new PasswordHasher<IdentityUser>(),
            [], [new PasswordValidator<IdentityUser>()], new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), _services, NullLogger<UserManager<IdentityUser>>.Instance);
        if (hasPassword) User.PasswordHash = Users.PasswordHasher.HashPassword(User, "Current1!");
        SignIn = new RecordingSignInManager(Users, options);
        Controller = new IdentityPasswordApiController(Users, SignIn, NullLogger<IdentityPasswordApiController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, User.Id)], IdentityConstants.ApplicationScheme))
                }
            }
        };
    }

    public void Dispose() { Users.Dispose(); _services.Dispose(); }
}

internal sealed class RecordingSignInManager(UserManager<IdentityUser> users, IOptions<IdentityOptions> options)
    : SignInManager<IdentityUser>(users, new HttpContextAccessor(), new UserClaimsPrincipalFactory<IdentityUser>(users, options),
        options, NullLogger<SignInManager<IdentityUser>>.Instance,
        new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())), new DefaultUserConfirmation<IdentityUser>())
{
    public int RefreshCount { get; private set; }
    public IdentityUser? TwoFactorUser { get; set; }
    public Microsoft.AspNetCore.Identity.SignInResult TwoFactorResult { get; set; } = Microsoft.AspNetCore.Identity.SignInResult.Failed;
    public string? SubmittedCode { get; private set; }
    public bool RememberMe { get; private set; }
    public bool RememberMachine { get; private set; }
    public override Task RefreshSignInAsync(IdentityUser user) { RefreshCount++; return Task.CompletedTask; }
    public override Task<IdentityUser?> GetTwoFactorAuthenticationUserAsync() => Task.FromResult(TwoFactorUser);
    public override Task<Microsoft.AspNetCore.Identity.SignInResult> TwoFactorAuthenticatorSignInAsync(string code, bool isPersistent, bool rememberClient)
    {
        SubmittedCode = code; RememberMe = isPersistent; RememberMachine = rememberClient;
        return Task.FromResult(TwoFactorResult);
    }
    public override Task<Microsoft.AspNetCore.Identity.SignInResult> TwoFactorRecoveryCodeSignInAsync(string recoveryCode)
    {
        SubmittedCode = recoveryCode;
        return Task.FromResult(TwoFactorResult);
    }
}

internal sealed class PasswordUserStore(IdentityUser user) : IUserPasswordStore<IdentityUser>
{
    public Task<string> GetUserIdAsync(IdentityUser account, CancellationToken cancellationToken) => Task.FromResult(account.Id);
    public Task<string?> GetUserNameAsync(IdentityUser account, CancellationToken cancellationToken) => Task.FromResult(account.UserName);
    public Task SetUserNameAsync(IdentityUser account, string? name, CancellationToken cancellationToken) { account.UserName = name; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(IdentityUser account, CancellationToken cancellationToken) => Task.FromResult(account.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(IdentityUser account, string? name, CancellationToken cancellationToken) { account.NormalizedUserName = name; return Task.CompletedTask; }
    public Task<IdentityResult> CreateAsync(IdentityUser account, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> UpdateAsync(IdentityUser account, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityResult> DeleteAsync(IdentityUser account, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    public Task<IdentityUser?> FindByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(id == user.Id ? user : null);
    public Task<IdentityUser?> FindByNameAsync(string name, CancellationToken cancellationToken) => Task.FromResult(name == user.NormalizedUserName ? user : null);
    public Task SetPasswordHashAsync(IdentityUser account, string? hash, CancellationToken cancellationToken) { account.PasswordHash = hash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(IdentityUser account, CancellationToken cancellationToken) => Task.FromResult(account.PasswordHash);
    public Task<bool> HasPasswordAsync(IdentityUser account, CancellationToken cancellationToken) => Task.FromResult(account.PasswordHash is not null);
    public void Dispose() { }
}
