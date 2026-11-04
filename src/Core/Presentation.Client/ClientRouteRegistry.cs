using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace OrbiApp.Presentation.Client;

public static class ClientRouteRegistry
{
    private static readonly HashSet<string> Routes = typeof(ClientRouteRegistry).Assembly.GetTypes()
        .SelectMany(type => type.GetCustomAttributes<RouteAttribute>())
        .Select(route => route.Template.TrimEnd('/'))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool Contains(string target, string baseUri)
    {
        var origin = new Uri(baseUri);
        if (!Uri.TryCreate(origin, target, out var destination) || !origin.IsBaseOf(destination)) return false;
        return Routes.Contains(Uri.UnescapeDataString(destination.AbsolutePath).TrimEnd('/'));
    }
}
