using System.ComponentModel.DataAnnotations;

namespace OrbiApp.Features.Identity.Client.Contracts;

public sealed record IdentityPasswordStatusResponse(bool HasPassword);

public class SetIdentityPasswordRequest
{
    [Required(ErrorMessage = "Ingresa tu nueva contraseña.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres.")]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "La contraseña y su confirmación no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ChangeIdentityPasswordRequest : SetIdentityPasswordRequest
{
    [Required(ErrorMessage = "Ingresa tu contraseña actual.")]
    public string OldPassword { get; set; } = string.Empty;
}

public sealed record IdentityPasswordActionResponse(string Message, IReadOnlyList<string>? Errors = null);
