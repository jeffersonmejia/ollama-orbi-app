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
        services.AddDbContextPool<ApplicationDbContext>(options => options
            // Do not replay arbitrary SaveChanges/transactions after an ambiguous connection failure.
            // Safe startup work and external GET requests have their own bounded retry policies.
            .UseNpgsql(connectionString, postgres => postgres.CommandTimeout(5))
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)),
            poolSize: 128);
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
        }).AddHttpMessageHandler<ExternalMarketResilienceHandler>();
        services.AddSingleton<ExternalMarketCircuitState>();
        services.AddTransient<ExternalMarketResilienceHandler>();
        services.AddTransient<IMarketPriceProvider>(provider => provider.GetRequiredService<ExternalMarketPriceService>());
        services.AddScoped<ISuggestMarketPriceUseCase, SuggestMarketPriceUseCase>();

        services.Configure<PayPalSettings>(configuration.GetSection("PayPal"));
        services.AddHttpClient<PayPalService>(client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddTransient<IPayPalGateway>(provider => provider.GetRequiredService<PayPalService>());

        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddTransient<OrbiApp.Application.Ports.IEmailSender, GmailEmailSender>();
        services.AddHostedService<EmailQueueWorker>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<IBackupManager>(provider => provider.GetRequiredService<BackupService>());
        services.AddHostedService(provider => provider.GetRequiredService<BackupService>());
        services.AddHostedService<DatabaseInitializationHostedService>();
        return services;
    }
}
