using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Application.Features.Shared.Ports;

namespace OrbiApp.Data;

public sealed class ApplicationDataSession(ApplicationDbContext context) : IApplicationDataSession
{
    public IEntitySet<T> Set<T>() where T : class => EfEntitySet<T>.Create(context.Set<T>());
    public void Add<T>(T entity) where T : class => context.Add(entity);
    public void Remove<T>(T entity) where T : class => context.Remove(entity);
    public void Update<T>(T entity) where T : class => context.Update(entity);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);
}

internal sealed class EfEntitySet<T>(DbSet<T> dbSet) : IEntitySet<T> where T : class
{
    public static IEntitySet<T> Create(DbSet<T> dbSet) => new EfEntitySet<T>(dbSet);
    public Type ElementType => ((IQueryable<T>)dbSet).ElementType;
    public Expression Expression => ((IQueryable<T>)dbSet).Expression;
    public IQueryProvider Provider => ((IQueryable<T>)dbSet).Provider;
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)dbSet).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Add(T entity) => dbSet.Add(entity);
    public void Remove(T entity) => dbSet.Remove(entity);
    public void RemoveRange(IEnumerable<T> entities) => dbSet.RemoveRange(entities);
    public ValueTask<T?> FindAsync(params object?[] keyValues) => dbSet.FindAsync(keyValues);
}
