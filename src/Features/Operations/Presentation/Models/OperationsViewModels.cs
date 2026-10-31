using OrbiApp.Models.Delivery;
using OrbiApp.Models;

namespace OrbiApp.Models.Operations;

public sealed class OperationsDashboardViewModel
{
    public int InventoryMovements { get; init; }
    public int ActiveReservations { get; init; }
    public int OpenIncidents { get; init; }
    public int StatusChanges { get; init; }
    public int PendingEmails { get; init; }
    public int AuditEvents { get; init; }
}

public sealed class InventoryMovementsViewModel
{
    public PaginatedList<InventoryMovement> Movements { get; init; } = new(new(), 0, 1, 5);
    public IReadOnlyList<DeliveryProduct> Products { get; init; } = Array.Empty<DeliveryProduct>();
    public IReadOnlyList<DeliveryOrder> Orders { get; init; } = Array.Empty<DeliveryOrder>();
}

public sealed class StockReservationsViewModel
{
    public PaginatedList<StockReservation> Reservations { get; init; } = new(new(), 0, 1, 5);
    public IReadOnlyList<DeliveryProduct> Products { get; init; } = Array.Empty<DeliveryProduct>();
    public IReadOnlyList<DeliveryOrder> Orders { get; init; } = Array.Empty<DeliveryOrder>();
}

public sealed class DeliveryIncidentsViewModel
{
    public PaginatedList<DeliveryIncident> Incidents { get; init; } = new(new(), 0, 1, 5);
    public IReadOnlyList<DeliveryOrder> Orders { get; init; } = Array.Empty<DeliveryOrder>();
}
