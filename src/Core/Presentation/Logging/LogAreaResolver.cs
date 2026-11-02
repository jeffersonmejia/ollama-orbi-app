using Serilog.Core;
using Serilog.Events;

namespace OrbiApp.Presentation.Logging;

public static class LogAreaResolver
{
    private static readonly HashSet<string> Areas = new(StringComparer.OrdinalIgnoreCase)
    {
        "Assistant",
        "Delivery",
        "Home",
        "Identity",
        "Notifications",
        "Operations",
        "Payments"
    };

    public static string FromPath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return "Home";

        var segment = segments[0];
        if (string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
            segment = segments[1];

        if (string.Equals(segment, "Account", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "Profile", StringComparison.OrdinalIgnoreCase))
        {
            return "Identity";
        }

        return Areas.FirstOrDefault(area => string.Equals(area, segment, StringComparison.OrdinalIgnoreCase))
            ?? "Core";
    }

    public static string FromCategory(string category)
    {
        var segments = category.Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var area = Areas.FirstOrDefault(candidate =>
                string.Equals(candidate, segment, StringComparison.OrdinalIgnoreCase));
            if (area is not null)
                return area;
        }

        var typeName = segments.LastOrDefault();
        if (typeName?.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) == true)
        {
            var controllerArea = typeName[..^"Controller".Length];
            var area = Areas.FirstOrDefault(candidate =>
                string.Equals(candidate, controllerArea, StringComparison.OrdinalIgnoreCase));
            if (area is not null)
                return area;
        }

        if (category.Contains("Identity", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("Account", StringComparison.OrdinalIgnoreCase))
        {
            return "Identity";
        }

        return "Core";
    }
}

public sealed class LogAreaEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.ContainsKey("LogArea"))
            return;

        var category = logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
            ? sourceContext.ToString().Trim('"')
            : string.Empty;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "LogArea",
            LogAreaResolver.FromCategory(category)));
    }
}
