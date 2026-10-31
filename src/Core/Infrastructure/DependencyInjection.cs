using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrbiApp.Application.Features.Assistant.Ports;
using OrbiApp.Application.Features.Delivery.Ports;
using OrbiApp.Application.Features.Operations.Ports;
using OrbiApp.Application.Features.Payments.Ports;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Application.Features.Delivery.UseCases;
using OrbiApp.Data;
using OrbiApp.Infrastructure.Features.Assistant;
using OrbiApp.Infrastructure.Features.Delivery;
using OrbiApp.Infrastructure.Features.Notifications;
using OrbiApp.Infrastructure.Features.Notifications.Settings;
using OrbiApp.Infrastructure.Features.Operations;
using OrbiApp.Infrastructure.Features.Payments;
using OrbiApp.Infrastructure.Features.Payments.Settings;

namespace OrbiApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrbiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options => options
            .UseNpgsql(connectionString, postgres => postgres
                .EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null)
                .CommandTimeout(60))
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
        services.AddScoped<IApplicationDataSession, ApplicationDataSession>();

        services.AddHttpClient<OllamaProductService>(client =>
        {
            var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(45);
        });
        services.AddTransient<IProductAssistant>(provider => provider.GetRequiredService<OllamaProductService>());

        services.AddHttpClient<ExternalMarketPriceService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OrbiApp-MarketComparison/1.0");
        });
        services.AddTransient<IMarketPriceProvider>(provider => provider.GetRequiredService<ExternalMarketPriceService>());
        services.AddScoped<ISuggestMarketPriceUseCase, SuggestMarketPriceUseCase>();

        services.Configure<PayPalSettings>(configuration.GetSection("PayPal"));
        services.AddHttpClient<PayPalService>();
        services.AddTransient<IPayPalGateway>(provider => provider.GetRequiredService<PayPalService>());

        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddTransient<IEmailSender<IdentityUser>, GmailEmailSender>();
        services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, GmailEmailSender>();
        services.AddHostedService<EmailQueueWorker>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<IBackupManager>(provider => provider.GetRequiredService<BackupService>());
        services.AddHostedService(provider => provider.GetRequiredService<BackupService>());
        return services;
    }
}
