using Microsoft.EntityFrameworkCore;
using OrbiApp.Data;
using OrbiApp.Models.Delivery;

namespace OrbiApp.Integration.Tests;

public static class DatabaseTestQueries
{
    public const string MissingEmail = "query-map-no-record@orbi.invalid";
    public const string MissingUserId = "query-map-no-user";

    public static IQueryable<DeliveryProduct> Catalog(ApplicationDbContext db) => db.DeliveryProducts
        .AsNoTracking()
        .Include(product => product.Store).ThenInclude(store => store!.Province)
        .Include(product => product.Store).ThenInclude(store => store!.City)
        .Where(product => product.IsAvailable && product.Store.IsActive);

    public static double Percentile(double[] sortedValues, double percentile) =>
        sortedValues[Math.Clamp((int)Math.Ceiling(percentile * sortedValues.Length) - 1, 0, sortedValues.Length - 1)];
}
