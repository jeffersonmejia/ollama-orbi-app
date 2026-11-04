using System.ComponentModel.DataAnnotations;

namespace OrbiApp.Features.Identity.Client.Contracts;

public sealed record TwoFactorChallengeResponse(bool ChallengePending);
public sealed record TwoFactorSignInError(string Message);

public sealed class AuthenticatorSignInRequest
{
    [Required, StringLength(7, MinimumLength = 6)]
    public string TwoFactorCode { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public bool RememberMachine { get; set; }
}

public sealed class RecoveryCodeSignInRequest
{
    [Required]
    public string RecoveryCode { get; set; } = string.Empty;
}
