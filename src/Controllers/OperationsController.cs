using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SakilaApp.Data;
using SakilaApp.Models.Delivery;
using SakilaApp.Models.Operations;
using SakilaApp.Models;

namespace SakilaApp.Controllers;

[Authorize(Roles = "Administrador,Vendedor,Repartidor,Usuario")]
public class OperationsController : Controller
{
    private static readonly string[] MovementTypes =
        { "Entrada", "Salida", "Ajuste", "Reserva", "Liberación" };
    private static readonly string[] IncidentSeverities =
        { "Baja", "Media", "Alta", "Crítica" };
    private static readonly string[] IncidentStatuses =
        { "Abierto", "En revisión", "Resuelto", "Cerrado" };

    private readonly ApplicationDbContext _context;

    public OperationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? section)
    {
        ViewBag.ActiveSection = section ?? "pedidos";
        var model = new OperationsDashboardViewModel();

        if (User.IsInRole("Administrador"))
        {
            model = new OperationsDashboardViewModel
            {
                InventoryMovements = await _context.InventoryMovements.CountAsync(),
                ActiveReservations = await _context.StockReservations.CountAsync(item => item.Status == "Activa"),
                OpenIncidents = await _context.DeliveryIncidents.CountAsync(item => item.Status == "Abierto" || item.Status == "En revisión"),
                StatusChanges = await _context.OrderStatusHistories.CountAsync(),
                PendingEmails = await _context.EmailQueue.CountAsync(item => item.Status == "Pendiente" || item.Status == "Fallido"),
                AuditEvents = await _context.AuditLogs.CountAsync()
            };
        }
        else if (User.IsInRole("Vendedor"))
        {
            model = new OperationsDashboardViewModel
            {
                InventoryMovements = await _context.InventoryMovements.CountAsync(),
                ActiveReservations = await _context.StockReservations.CountAsync(item => item.Status == "Activa")
            };
        }
        else
        {
            var orders = AccessibleOrders();
            model = new OperationsDashboardViewModel
            {
                OpenIncidents = await _context.DeliveryIncidents
                    .Where(incident => orders.Any(order => order.DeliveryOrderId == incident.DeliveryOrderId))
                    .CountAsync(incident => incident.Status == "Abierto" || incident.Status == "En revisión"),
                StatusChanges = await _context.OrderStatusHistories
                    .CountAsync(history => orders.Any(order => order.DeliveryOrderId == history.DeliveryOrderId))
            };
        }

        return View(model);
    }

    [Authorize(Roles = "Administrador,Vendedor")]
    public async Task<IActionResult> InventoryMovements(int page = 1)
    {
        var model = new InventoryMovementsViewModel
        {
            Movements = await PaginatedList<InventoryMovement>.CreateAsync(_context.InventoryMovements
                .AsNoTracking()
                .Include(item => item.Product).ThenInclude(product => product.Store)
                .Include(item => item.Order)
                .Include(item => item.PerformedByUser)
                .OrderByDescending(item => item.CreatedAt), Math.Max(1, page), 5),
            Products = await _context.DeliveryProducts.AsNoTracking()
                .Include(product => product.Store)
                .OrderBy(product => product.Store.Name).ThenBy(product => product.Name)
                .ToListAsync(),
            Orders = await _context.DeliveryOrders.AsNoTracking()
                .OrderByDescending(order => order.CreatedAt).Take(100).ToListAsync()
        };
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInventoryMovement(
        int deliveryProductId,
        int? deliveryOrderId,
        string movementType,
        int quantityDelta,
        decimal? unitCost,
        string? metadata)
    {
        if (!MovementTypes.Contains(movementType) || quantityDelta == 0 || unitCost < 0 ||
            !await _context.DeliveryProducts.AnyAsync(item => item.DeliveryProductId == deliveryProductId) ||
            deliveryOrderId.HasValue && !await _context.DeliveryOrders.AnyAsync(item => item.DeliveryOrderId == deliveryOrderId))
        {
            TempData["Error"] = "Revisa el producto, el tipo, la cantidad y el pedido relacionado.";
            return RedirectToAction(nameof(InventoryMovements));
        }

        var normalizedMetadata = NormalizeJson(metadata);
        if (normalizedMetadata is null)
        {
            TempData["Error"] = "Los metadatos deben tener formato JSON válido.";
            return RedirectToAction(nameof(InventoryMovements));
        }

        var movement = new InventoryMovement
        {
            DeliveryProductId = deliveryProductId,
            DeliveryOrderId = deliveryOrderId,
            PerformedByUserId = CurrentUserId,
            MovementType = movementType,
            QuantityDelta = quantityDelta,
            UnitCost = unitCost,
            MetadataJson = normalizedMetadata
        };
        _context.InventoryMovements.Add(movement);
        AddAudit("Crear", "inventory_movement", null, movement);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Movimiento de inventario registrado.";
        return RedirectToAction(nameof(InventoryMovements));
    }

    [Authorize(Roles = "Administrador,Vendedor")]
    public async Task<IActionResult> StockReservations(int page = 1)
    {
        var model = new StockReservationsViewModel
        {
            Reservations = await PaginatedList<StockReservation>.CreateAsync(_context.StockReservations
                .AsNoTracking()
                .Include(item => item.Product).ThenInclude(product => product.Store)
                .Include(item => item.Order)
                .Include(item => item.ReservedByUser)
                .OrderByDescending(item => item.CreatedAt), Math.Max(1, page), 5),
            Products = await _context.DeliveryProducts.AsNoTracking()
                .Include(product => product.Store)
                .OrderBy(product => product.Store.Name).ThenBy(product => product.Name)
                .ToListAsync(),
            Orders = await _context.DeliveryOrders.AsNoTracking()
                .OrderByDescending(order => order.CreatedAt).Take(100).ToListAsync()
        };
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStockReservation(
        int deliveryProductId,
        int deliveryOrderId,
        int quantity,
        DateTimeOffset expiresAt)
    {
        if (quantity < 1 || expiresAt <= DateTimeOffset.UtcNow ||
            !await _context.DeliveryProducts.AnyAsync(item => item.DeliveryProductId == deliveryProductId) ||
            !await _context.DeliveryOrders.AnyAsync(item => item.DeliveryOrderId == deliveryOrderId))
        {
            TempData["Error"] = "La reserva necesita producto, pedido, cantidad y vencimiento válidos.";
            return RedirectToAction(nameof(StockReservations));
        }

        if (await _context.StockReservations.AnyAsync(item =>
                item.DeliveryProductId == deliveryProductId &&
                item.DeliveryOrderId == deliveryOrderId &&
                item.Status == "Activa"))
        {
            TempData["Error"] = "Ya existe una reserva activa para ese producto y pedido.";
            return RedirectToAction(nameof(StockReservations));
        }

        var reservation = new StockReservation
        {
            DeliveryProductId = deliveryProductId,
            DeliveryOrderId = deliveryOrderId,
            ReservedByUserId = CurrentUserId,
            Quantity = quantity,
            ExpiresAt = expiresAt.ToUniversalTime()
        };
        _context.StockReservations.Add(reservation);
        AddAudit("Crear", "stock_reservation", null, reservation);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Reserva de stock creada.";
        return RedirectToAction(nameof(StockReservations));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Vendedor")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseStockReservation(long id)
    {
        var reservation = await _context.StockReservations.FindAsync(id);
        if (reservation is null) return NotFound();
        if (reservation.Status != "Activa")
        {
            TempData["Error"] = "La reserva ya no está activa.";
            return RedirectToAction(nameof(StockReservations));
        }

        reservation.Status = "Liberada";
        reservation.ReleasedAt = DateTimeOffset.UtcNow;
        AddAudit("Liberar", "stock_reservation", reservation.StockReservationId.ToString(), new { reservation.Status, reservation.ReleasedAt });
        await _context.SaveChangesAsync();
        TempData["Success"] = "Reserva liberada.";
        return RedirectToAction(nameof(StockReservations));
    }

    [Authorize(Roles = "Administrador,Repartidor,Usuario")]
    public async Task<IActionResult> DeliveryIncidents(int page = 1)
    {
        List<int> orderIds;
        IQueryable<DeliveryOrder> ordersQuery;
        if (User.IsInRole("Administrador"))
        {
            orderIds = new List<int>();
            ordersQuery = _context.DeliveryOrders.AsNoTracking();
        }
        else
        {
            orderIds = await AccessibleOrders().Select(o => o.DeliveryOrderId).ToListAsync();
            ordersQuery = _context.DeliveryOrders.AsNoTracking().Where(o => orderIds.Contains(o.DeliveryOrderId));
        }
        var model = new DeliveryIncidentsViewModel
        {
            Incidents = await PaginatedList<DeliveryIncident>.CreateAsync(_context.DeliveryIncidents
                .AsNoTracking()
                .Where(incident => User.IsInRole("Administrador") || orderIds.Contains(incident.DeliveryOrderId))
                .Include(incident => incident.Order).ThenInclude(order => order.Store)
                .Include(incident => incident.ReportedByUser)
                .OrderByDescending(incident => incident.CreatedAt), Math.Max(1, page), 12),
            Orders = await ordersQuery.Include(order => order.Store)
                .OrderByDescending(order => order.CreatedAt).Take(100).ToListAsync()
        };
        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Repartidor,Usuario")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDeliveryIncident(
        int deliveryOrderId,
        string incidentType,
        string severity,
        string description)
    {
        if (!await CanAccessOrderAsync(deliveryOrderId)) return Forbid();
        if (string.IsNullOrWhiteSpace(incidentType) || incidentType.Length > 60 ||
            !IncidentSeverities.Contains(severity) ||
            string.IsNullOrWhiteSpace(description))
        {
            TempData["Error"] = "Completa correctamente el tipo, la severidad y la descripción.";
            return RedirectToAction(nameof(DeliveryIncidents));
        }

        var incident = new DeliveryIncident
        {
            DeliveryOrderId = deliveryOrderId,
            ReportedByUserId = CurrentUserId,
            IncidentType = incidentType.Trim(),
            Severity = severity,
            Description = description.Trim()
        };
        _context.DeliveryIncidents.Add(incident);
        AddAudit("Crear", "delivery_incident", null, incident);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Incidencia reportada.";
        return RedirectToAction(nameof(DeliveryIncidents));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDeliveryIncident(long id, string status)
    {
        if (!IncidentStatuses.Contains(status)) return BadRequest();
        var incident = await _context.DeliveryIncidents.FindAsync(id);
        if (incident is null) return NotFound();

        incident.Status = status;
        incident.ResolvedAt = status is "Resuelto" or "Cerrado" ? DateTimeOffset.UtcNow : null;
        AddAudit("Actualizar estado", "delivery_incident", incident.DeliveryIncidentId.ToString(), new { incident.Status, incident.ResolvedAt });
        await _context.SaveChangesAsync();
        TempData["Success"] = "Estado de la incidencia actualizado.";
        return RedirectToAction(nameof(DeliveryIncidents));
    }

    [Authorize(Roles = "Administrador,Repartidor,Usuario")]
    public async Task<IActionResult> OrderStatusHistory(int? orderId, string? status, int page = 1)
    {
        IQueryable<OrderStatusHistory> query;
        if (User.IsInRole("Administrador"))
        {
            query = _context.OrderStatusHistories.AsNoTracking();
        }
        else
        {
            var orderIds = await AccessibleOrders().Select(o => o.DeliveryOrderId).ToListAsync();
            query = _context.OrderStatusHistories.AsNoTracking()
                .Where(h => orderIds.Contains(h.DeliveryOrderId));
        }
        query = query
            .Include(history => history.Order).ThenInclude(order => order.Store)
            .Include(history => history.ChangedByUser);
        if (orderId.HasValue) query = query.Where(item => item.DeliveryOrderId == orderId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.NewStatus == status);
        ViewBag.OrderId = orderId;
        ViewBag.Status = status;
        var histories = await PaginatedList<OrderStatusHistory>.CreateAsync(query.OrderByDescending(history => history.ChangedAt), Math.Max(1, page), 12);
        return View(histories);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> AuditLogs(string? buscar, int page = 1)
    {
        var query = _context.AuditLogs.AsNoTracking().Include(item => item.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var pattern = $"%{buscar.Trim()}%";
            query = query.Where(item => EF.Functions.ILike(item.Action, pattern) ||
                EF.Functions.ILike(item.EntityType, pattern) ||
                (item.EntityId != null && EF.Functions.ILike(item.EntityId, pattern)) ||
                (item.User != null && EF.Functions.ILike(item.User.Email!, pattern)));
        }
        ViewBag.Buscar = buscar;
        return View(await PaginatedList<AuditLog>.CreateAsync(query.OrderByDescending(item => item.CreatedAt), Math.Max(1, page), 5));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> EmailQueue(int page = 1)
    {
        return View(await PaginatedList<EmailQueueItem>.CreateAsync(_context.EmailQueue.AsNoTracking()
            .OrderByDescending(item => item.CreatedAt), Math.Max(1, page), 5));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnqueueEmail(string recipientEmail, string subject, string bodyHtml, DateTimeOffset? scheduledAt)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@') ||
            string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(bodyHtml))
        {
            TempData["Error"] = "Completa destinatario, asunto y contenido.";
            return RedirectToAction(nameof(EmailQueue));
        }

        var email = new EmailQueueItem
        {
            RecipientEmail = recipientEmail.Trim(),
            Subject = subject.Trim(),
            BodyHtml = bodyHtml,
            ScheduledAt = (scheduledAt ?? DateTimeOffset.UtcNow).ToUniversalTime()
        };
        _context.EmailQueue.Add(email);
        AddAudit("Encolar", "email_queue", null, new { email.RecipientEmail, email.Subject, email.ScheduledAt });
        await _context.SaveChangesAsync();
        TempData["Success"] = "Correo agregado a la cola.";
        return RedirectToAction(nameof(EmailQueue));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryEmail(long id)
    {
        var email = await _context.EmailQueue.FindAsync(id);
        if (email is null) return NotFound();
        email.Status = "Pendiente";
        email.ScheduledAt = DateTimeOffset.UtcNow;
        email.LastError = null;
        AddAudit("Reintentar", "email_queue", id.ToString(), new { email.Status, email.ScheduledAt });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(EmailQueue));
    }

    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEmail(long id)
    {
        var email = await _context.EmailQueue.FindAsync(id);
        if (email is null) return NotFound();
        if (email.Status != "Enviado") email.Status = "Cancelado";
        AddAudit("Cancelar", "email_queue", id.ToString(), new { email.Status });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(EmailQueue));
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private IQueryable<DeliveryOrder> AccessibleOrders()
    {
        var query = _context.DeliveryOrders.AsQueryable();
        if (User.IsInRole("Administrador")) return query;

        var email = User.Identity?.Name ?? string.Empty;
        if (User.IsInRole("Repartidor")) return query.Where(order => order.DeliveryPersonEmail == email);
        if (User.IsInRole("Usuario")) return query.Where(order => order.CustomerEmail == email);
        return query.Where(_ => false);
    }

    private Task<bool> CanAccessOrderAsync(int orderId) =>
        AccessibleOrders().AnyAsync(order => order.DeliveryOrderId == orderId);

    private void AddAudit(string action, string entityType, string? entityId, object? newValues)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = CurrentUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = userAgent.Length > 512 ? userAgent[..512] : userAgent,
            CorrelationId = HttpContext.TraceIdentifier
        });
    }

    private static string? NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "{}";
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
