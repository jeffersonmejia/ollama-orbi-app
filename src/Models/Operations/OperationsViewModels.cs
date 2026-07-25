using SakilaApp.Models.Delivery;

namespace SakilaApp.Models.Operations;

public sealed class OperationsDashboardViewModel
{
    public int InventoryMovements { get; init; }
    public int ActiveReservations { get; init; }
    public int OpenIncidents { get; init; }
    public int StatusChanges { get; init; }
    public int PendingEmails { get; init; }
    public int AuditEvents { get; init; }
    public int AiRequests { get; init; }
}

public sealed class InventoryMovementsViewModel
{
    public IReadOnlyList<InventoryMovement> Movements { get; init; } = Array.Empty<InventoryMovement>();
    public IReadOnlyList<DeliveryProduct> Products { get; init; } = Array.Empty<DeliveryProduct>();
    public IReadOnlyList<DeliveryOrder> Orders { get; init; } = Array.Empty<DeliveryOrder>();
}

public sealed class StockReservationsViewModel
{
    public IReadOnlyList<StockReservation> Reservations { get; init; } = Array.Empty<StockReservation>();
    public IReadOnlyList<DeliveryProduct> Products { get; init; } = Array.Empty<DeliveryProduct>();
    public IReadOnlyList<DeliveryOrder> Orders { get; init; } = Array.Empty<DeliveryOrder>();
}

public sealed class DeliveryIncidentsViewModel
{
    public IReadOnlyList<DeliveryIncident> Incidents { get; init; } = Array.Empty<DeliveryIncident>();
    public IReadOnlyList<DeliveryOrder> Orders { get; init; } = Array.Empty<DeliveryOrder>();
}
