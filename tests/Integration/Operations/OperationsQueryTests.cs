using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrbiApp.Integration.Tests;

[Collection(PostgresDatabaseCollection.Name)]
public sealed class OperationsQueryTests(PostgresDatabaseFixture fixture)
{
    [Fact]
    public async Task Orders_payments_inventory_and_operations_reads_execute()
    {
        await using var db = fixture.CreateContext();
        var customerOrders = await db.DeliveryOrders.AsNoTracking().Include(order => order.Store).Include(order => order.Items)
            .Where(order => order.CustomerEmail == DatabaseTestQueries.MissingEmail)
            .OrderByDescending(order => order.CreatedAt).Take(12).ToListAsync();
        var driverOrders = await db.DeliveryOrders.AsNoTracking().Include(order => order.Store)
            .Where(order => order.DeliveryPersonEmail == DatabaseTestQueries.MissingEmail && order.Status != "Cancelado")
            .OrderByDescending(order => order.CreatedAt).Take(12).ToListAsync();
        var customerPayments = await db.DeliveryPayments.AsNoTracking().Include(payment => payment.Order)
            .ThenInclude(order => order.Store).Where(payment => payment.Order.CustomerEmail == DatabaseTestQueries.MissingEmail)
            .OrderByDescending(payment => payment.CreatedAt).Take(12).ToListAsync();
        var movements = await db.InventoryMovements.AsNoTracking().Include(movement => movement.Product)
            .ThenInclude(product => product.Store).Where(movement => movement.PerformedByUserId == DatabaseTestQueries.MissingUserId)
            .OrderByDescending(movement => movement.CreatedAt).Take(12).ToListAsync();
        var openIncidents = await db.DeliveryIncidents.CountAsync(incident => incident.Status == "Abierto" || incident.Status == "En revisión");
        var activeReservations = await db.StockReservations.CountAsync(reservation => reservation.Status == "Activa");
        var auditEvents = await db.AuditLogs.AsNoTracking().OrderByDescending(log => log.CreatedAt).Take(12).ToListAsync();
        var statusHistory = await db.OrderStatusHistories.AsNoTracking().OrderByDescending(history => history.ChangedAt).Take(12).ToListAsync();

        Assert.Empty(customerOrders);
        Assert.Empty(driverOrders);
        Assert.Empty(customerPayments);
        Assert.Empty(movements);
        Assert.True(openIncidents >= 0);
        Assert.True(activeReservations >= 0);
        Assert.NotNull(auditEvents);
        Assert.NotNull(statusHistory);
    }
}
