using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OrbiApp.Data;
using OrbiApp.Infrastructure;
using OrbiApp.Presentation.Client.Networking;
using OrbiApp.Presentation.Features;
using OrbiApp.Features.Identity.Presentation.DependencyInjection;

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
        services.AddMemoryCache();
        services.AddIdentityPresentation();
        services.AddRazorComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization();
        services.AddCascadingAuthenticationState();
        services.AddHttpContextAccessor();
        services.AddLogging(logging =>
        {
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Connection", LogLevel.Information);
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddSingleton<DatabaseReadinessState>();
        services.AddHealthChecks()
            .AddCheck<DatabaseAvailabilityHealthCheck>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        ConfigurePerformanceMetrics(services, configuration);

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
        services.AddScoped<HttpClient>(serviceProvider =>
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("OrbiApi"));
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

    private static void ConfigurePerformanceMetrics(IServiceCollection services, IConfiguration configuration)
    {
        var endpointValue = configuration["Observability:Metrics:OtlpEndpoint"];
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint)
            || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            return;
        }

        var exportIntervalMilliseconds = Math.Clamp(
            configuration.GetValue("Observability:Metrics:ExportIntervalMilliseconds", 5_000),
            1_000,
            60_000);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("Dash"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = endpoint;
                    exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = exportIntervalMilliseconds;
                }));
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

    private static IServiceCollection AddOrbiIdentity(this IServiceCollection services)
    {
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequiredLength = 6;
        })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.AccessDeniedPath = "/Identity/Account/AccessDenied";
            options.Events.OnValidatePrincipal = async context =>
            {
                // ExceptionHandler re-executes the request through authentication. Avoid
                // querying a failed database again while rendering the public error page.
                if (!context.Request.Path.StartsWithSegments("/errors"))
                {
                    await SecurityStampValidator.ValidatePrincipalAsync(context);
                }
            };
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        return services;
    }

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
