namespace SakilaApp.Models;

public class AdminPanelMetric
{
    public required string Label { get; init; }

    public required string Detail { get; init; }

    public int Value { get; init; }

    public int Percent { get; init; }
}
