using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace OrbiApp.Features.Identity.Client.Authentication;

public static class IdentityClientAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityClientAuthentication(this IServiceCollection services)
    {
        services.AddAuthenticationStateDeserialization();
        // Keep the framework's persisted snapshot as the initial state, then allow
        // same-origin cookie login/logout to update that state without a document reload.
        var snapshot = services.Last(service => service.ServiceType == typeof(AuthenticationStateProvider));
        services.Remove(snapshot);
        services.AddScoped<SessionAuthenticationStateProvider>(provider =>
        {
            var initial = snapshot.ImplementationInstance
                ?? snapshot.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, snapshot.ImplementationType!);
            return new SessionAuthenticationStateProvider((AuthenticationStateProvider)initial, provider.GetRequiredService<HttpClient>());
        });
        services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<SessionAuthenticationStateProvider>());
        return services;
    }
}
