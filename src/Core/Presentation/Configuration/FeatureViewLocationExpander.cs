using Microsoft.AspNetCore.Mvc.Razor;

namespace OrbiApp.Presentation.Features;

public sealed class FeatureViewLocationExpander : IViewLocationExpander
{
    private static readonly IReadOnlyDictionary<string, string> FeatureByController =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Store"] = "Commerce",
            ["Payment"] = "Commerce",
            ["Delivery"] = "Delivery",
            ["Inventory"] = "Inventory",
            ["Operations"] = "Operations",
            ["Home"] = "Home"
        };

    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (context.AreaName is null && FeatureByController.TryGetValue(context.ControllerName, out var feature))
        {
            yield return $"/Features/{feature}/Presentation/Views/{{1}}/{{0}}.cshtml";
        }

        foreach (var location in viewLocations)
        {
            yield return location;
        }
    }
}
