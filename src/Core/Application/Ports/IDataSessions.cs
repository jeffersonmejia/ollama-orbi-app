namespace OrbiApp.Application.Features.Shared.Ports;

/// <summary>Persistence port; consumers know LINQ, but not the database provider.</summary>
public interface IEntitySet<T> : IQueryable<T> where T : class
{
    void Add(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
    ValueTask<T?> FindAsync(params object?[] keyValues);
}

public interface IDataSession
{
    IEntitySet<T> Set<T>() where T : class;
    void Add<T>(T entity) where T : class;
    void Remove<T>(T entity) where T : class;
    void Update<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationDataSession : IDataSession { }
