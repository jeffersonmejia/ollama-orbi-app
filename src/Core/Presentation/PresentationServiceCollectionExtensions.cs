using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using OrbiApp.Data;
using OrbiApp.Infrastructure;
using OrbiApp.Presentation.Features;

namespace OrbiApp.Presentation;

public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection AddOrbiPresentation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-EC");

        services.AddControllersWithViews()
            .AddRazorOptions(options => options.ViewLocationExpanders.Add(new FeatureViewLocationExpander()));
        services.AddRazorPages(options => options.RootDirectory = "/Features/Identity/Presentation");
        services.AddOrbiInfrastructure(configuration);
        services.AddDataProtection(configuration);
        services.AddOrbiIdentity();
        services.AddGoogleAuthentication(configuration, environment);

        return services;
    }

    private static IServiceCollection AddDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName("OrbiApp");
        }

        return services;
    }

    private static IServiceCollection AddOrbiIdentity(this IServiceCollection services) => services
        .AddDefaultIdentity<IdentityUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequiredLength = 6;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .Services;

    private static IServiceCollection AddGoogleAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddAuthentication()
            .AddGoogle(options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = "/signin-google";
                options.ClientId = configuration["Authentication:Google:ClientId"]!;
                options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
                options.Events.OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    context.Response.Redirect("/Identity/Account/Login?oauthError=google");
                    return Task.CompletedTask;
                };

                if (environment.IsDevelopment())
                {
                    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                }
            });

        return services;
    }
}
