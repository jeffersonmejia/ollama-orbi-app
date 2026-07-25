using Microsoft.AspNetCore.Identity;
using SakilaApp.Models.Delivery;

namespace SakilaApp.Models.Operations;

public class DeliveryIncident
{
    public long DeliveryIncidentId { get; set; }
    public int DeliveryOrderId { get; set; }
    public string? ReportedByUserId { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Media";
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Abierto";
    public string DetailsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }

    public DeliveryOrder Order { get; set; } = null!;
    public IdentityUser? ReportedByUser { get; set; }
}
