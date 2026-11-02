using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models.Identity;

namespace OrbiApp.Presentation.Services;

public sealed class DeliveryPageCache(IMemoryCache cache)
{
    private static readonly MemoryCacheEntryOptions EntryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(8)
    };

    private const string ProvincesKey = "delivery:locations:provinces";
    private const string CitiesKey = "delivery:locations:cities";
    private long _locationVersion;

    public Task<IReadOnlyList<EcuadorProvince>> GetProvincesAsync(
        IApplicationDataSession data,
        CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(ProvincesKey, async entry =>
        {
            entry.SetOptions(EntryOptions);
            return (IReadOnlyList<EcuadorProvince>)await data.Set<EcuadorProvince>()
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);
        })!;

    public Task<IReadOnlyList<EcuadorCity>> GetCitiesAsync(
        IApplicationDataSession data,
        CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(CitiesKey, async entry =>
        {
            entry.SetOptions(EntryOptions);
            return (IReadOnlyList<EcuadorCity>)await data.Set<EcuadorCity>()
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);
        })!;

    public Task<UserProfile?> GetUserProfileAsync(
        IApplicationDataSession data,
        string userId,
        CancellationToken cancellationToken = default) =>
        GetOrCreateUserValueAsync(
            UserProfileKey(userId),
            async () => await data.Set<UserProfile>()
                .AsNoTracking()
                .Include(item => item.Province)
                .Include(item => item.City)
                .FirstOrDefaultAsync(item => item.IdentityUserId == userId, cancellationToken));

    public Task<UserAddress?> GetPrimaryAddressAsync(
        IApplicationDataSession data,
        string userId,
        CancellationToken cancellationToken = default) =>
        GetOrCreateUserValueAsync(
            PrimaryAddressKey(userId),
            async () => await data.Set<UserAddress>()
                .AsNoTracking()
                .Include(item => item.Province)
                .Include(item => item.City)
                .Where(item => item.IdentityUserId == userId)
                .OrderByDescending(item => item.IsDefault)
                .ThenBy(item => item.UserAddressId)
                .FirstOrDefaultAsync(cancellationToken));

    private async Task<T?> GetOrCreateUserValueAsync<T>(string key, Func<Task<T?>> factory) where T : class
    {
        var cached = await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.SetOptions(EntryOptions);
            return new CachedValue<T>(await factory());
        });

        return cached?.Value;
    }

    public void InvalidateUser(string userId)
    {
        cache.Remove(UserProfileKey(userId));
        cache.Remove(PrimaryAddressKey(userId));
    }

    public void InvalidateLocations()
    {
        cache.Remove(ProvincesKey);
        cache.Remove(CitiesKey);
        Interlocked.Increment(ref _locationVersion);
    }

    private string UserProfileKey(string userId) => $"delivery:user-profile:{Volatile.Read(ref _locationVersion)}:{userId}";
    private string PrimaryAddressKey(string userId) => $"delivery:user-address:{Volatile.Read(ref _locationVersion)}:{userId}";

    private sealed record CachedValue<T>(T? Value) where T : class;
}
