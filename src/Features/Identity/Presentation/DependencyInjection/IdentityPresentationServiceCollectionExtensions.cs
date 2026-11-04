using Microsoft.Extensions.DependencyInjection;
using OrbiApp.Features.Identity.Presentation.Services;

namespace OrbiApp.Features.Identity.Presentation.DependencyInjection;

public static class IdentityPresentationServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityPresentation(this IServiceCollection services)
    {
        services.AddSingleton<IdentityPageCache>();
        return services;
    }
}
