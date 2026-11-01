using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using OrbiApp.Data;
using OrbiApp.Infrastructure;
using OrbiApp.Presentation.Client.Networking;
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
        services.AddRazorPages();
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();
        services.AddCascadingAuthenticationState();
        services.AddHttpContextAccessor();

        var apiTimeoutSeconds = Math.Clamp(configuration.GetValue("Blazor:RequestTimeoutSeconds", 30), 1, 300);
        var httpClientTimeoutSeconds = Math.Clamp(configuration.GetValue("Blazor:HttpClientTimeoutSeconds", 35), apiTimeoutSeconds + 1, 600);
        services.Configure<ApiTimeoutOptions>(options =>
        {
            options.RequestTimeoutSeconds = apiTimeoutSeconds;
            options.HttpClientTimeoutSeconds = httpClientTimeoutSeconds;
        });
        services.AddTransient<ApiTimeoutHandler>();
        services.AddHttpClient("OrbiApi", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(httpClientTimeoutSeconds);
        }).AddHttpMessageHandler<ApiTimeoutHandler>();
        services.AddRequestTimeouts(options =>
        {
            options.AddPolicy("api", TimeSpan.FromSeconds(apiTimeoutSeconds));
            options.AddPolicy("assistant", TimeSpan.FromSeconds(
                Math.Clamp(configuration.GetValue("Blazor:AssistantTimeoutSeconds", 120), 1, 600)));
        });

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
        var clientId = configuration["Authentication:Google:ClientId"];
        var clientSecret = configuration["Authentication:Google:ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return services;
        }

        services.AddAuthentication()
            .AddGoogle(options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = "/signin-google";
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
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
