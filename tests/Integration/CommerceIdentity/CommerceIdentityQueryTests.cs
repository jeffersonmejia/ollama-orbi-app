using Microsoft.EntityFrameworkCore;
using OrbiApp.Models.Commerce;
using Xunit;

namespace OrbiApp.Integration.Tests;

[Collection(PostgresDatabaseCollection.Name)]
public sealed class CommerceIdentityQueryTests(PostgresDatabaseFixture fixture)
{
    [Fact]
    public async Task Cart_address_and_location_reads_execute_with_expected_empty_user_results()
    {
        await using var db = fixture.CreateContext();
        var commerceProducts = await db.Products.AsNoTracking()
            .Where(product => product.IsActive && product.Stock > 0).OrderBy(product => product.Name)
            .Take(5).ToListAsync();
        var commerceCart = await db.ShoppingCartItems.AsNoTracking().Include(item => item.Product)
            .Where(item => item.UserEmail == DatabaseTestQueries.MissingEmail)
            .OrderBy(item => item.ShoppingCartItemId).Take(5).ToListAsync();
        var deliveryCart = await db.DeliveryCartItems.AsNoTracking().Include(item => item.Product)
            .Where(item => item.UserEmail == DatabaseTestQueries.MissingEmail)
            .OrderBy(item => item.CreatedAt).Take(5).ToListAsync();
        var purchaseOrders = await db.Set<PurchaseOrder>().AsNoTracking().Include(order => order.Details)
            .Where(order => order.UserEmail == DatabaseTestQueries.MissingEmail)
            .OrderByDescending(order => order.CreatedAt).Take(5).ToListAsync();
        var purchaseDetails = await db.Set<PurchaseOrderDetail>().AsNoTracking()
            .Where(detail => detail.PurchaseOrder.UserEmail == DatabaseTestQueries.MissingEmail).Take(5).ToListAsync();
        var commercePayments = await db.Set<PaymentTransaction>().AsNoTracking().Include(payment => payment.PurchaseOrder)
            .Where(payment => payment.PurchaseOrder.UserEmail == DatabaseTestQueries.MissingEmail)
            .OrderByDescending(payment => payment.CreatedAt).Take(5).ToListAsync();
        var identityUsers = await db.Users.AsNoTracking().Where(user => user.Email == DatabaseTestQueries.MissingEmail).Take(5).ToListAsync();
        var addresses = await db.UserAddresses.AsNoTracking().Include(address => address.Province).Include(address => address.City)
            .Where(address => address.IdentityUserId == DatabaseTestQueries.MissingUserId)
            .OrderByDescending(address => address.IsDefault).ThenBy(address => address.UserAddressId).ToListAsync();
        var provinces = await db.EcuadorProvinces.AsNoTracking().OrderBy(province => province.Name).Take(5).ToListAsync();
        var cities = await db.EcuadorCities.AsNoTracking().Where(city => city.Code == fixture.CityCode)
            .OrderBy(city => city.Name).Take(5).ToListAsync();

        Assert.NotNull(commerceProducts);
        Assert.Empty(commerceCart);
        Assert.Empty(deliveryCart);
        Assert.Empty(purchaseOrders);
        Assert.Empty(purchaseDetails);
        Assert.Empty(commercePayments);
        Assert.Empty(identityUsers);
        Assert.Empty(addresses);
        Assert.NotEmpty(provinces);
        Assert.NotEmpty(cities);
    }
}
