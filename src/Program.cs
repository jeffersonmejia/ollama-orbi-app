using OrbiApp.Data;
using OrbiApp.Infrastructure;
using OrbiApp.Presentation;
using OrbiApp.Presentation.Logging;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
var logDirectory = Path.Combine(builder.Environment.ContentRootPath, ".logs");
Directory.CreateDirectory(logDirectory);

builder.Host.UseSerilog((_, _, loggerConfiguration) => loggerConfiguration
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.With<LogAreaEnricher>()
    .WriteTo.Console()
    .WriteTo.Map("LogArea", "Core", (area, sinks) =>
    {
        var areaDirectory = Path.Combine(logDirectory, area);
        Directory.CreateDirectory(areaDirectory);
        var isDeliveryDiagnostics = string.Equals(area, "Delivery", StringComparison.OrdinalIgnoreCase);
        sinks.Logger(fileLogger => fileLogger
            .MinimumLevel.Is(isDeliveryDiagnostics ? LogEventLevel.Information : LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(areaDirectory, isDeliveryDiagnostics ? "diagnostics-.log" : "errors-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{LogArea}] [{SourceContext}] [{TraceId}] {Message:lj}{NewLine}{Exception}"));
    }, sinkMapCountLimit: 16));

builder.Services.AddOrbiPresentation(builder.Configuration, builder.Environment);

var app = builder.Build();
if (Uri.TryCreate(builder.Configuration["Observability:Metrics:OtlpEndpoint"], UriKind.Absolute, out var metricsEndpoint))
{
    app.Logger.LogInformation("Performance metrics export configured for {MetricsEndpoint}", metricsEndpoint);
}
else
{
    app.Logger.LogWarning("Performance metrics export is disabled because no OTLP endpoint is configured.");
}
app.UseOrbiPresentation();
app.Run();
