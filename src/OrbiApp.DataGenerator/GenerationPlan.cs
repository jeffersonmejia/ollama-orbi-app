namespace OrbiApp.DataGenerator;

internal sealed record GenerationPlan(
    int Stores,
    int Products,
    int Profiles,
    int Orders,
    int OrderItems,
    int Payments,
    int InventoryMovements,
    int AuditLogs,
    int Incidents)
{
    private static readonly (string Name, int Weight)[] Weights =
    {
        (nameof(Stores), 2_000),
        (nameof(Products), 80_000),
        (nameof(Profiles), 120_000),
        (nameof(Orders), 240_000),
        (nameof(OrderItems), 420_000),
        (nameof(Payments), 90_000),
        (nameof(InventoryMovements), 35_000),
        (nameof(AuditLogs), 10_000),
        (nameof(Incidents), 3_000)
    };

    public int Total => Stores + Products + Profiles + Orders + OrderItems + Payments + InventoryMovements + AuditLogs + Incidents;

    public static GenerationPlan Create(int total)
    {
        var counts = Weights.ToDictionary(x => x.Name, x => (int)((long)total * x.Weight / 1_000_000));
        var assigned = counts.Values.Sum();
        foreach (var item in Weights.OrderByDescending(x => ((long)total * x.Weight) % 1_000_000).Take(total - assigned))
            counts[item.Name]++;

        return new(
            counts[nameof(Stores)], counts[nameof(Products)], counts[nameof(Profiles)],
            counts[nameof(Orders)], counts[nameof(OrderItems)], counts[nameof(Payments)],
            counts[nameof(InventoryMovements)], counts[nameof(AuditLogs)], counts[nameof(Incidents)]);
    }
}
