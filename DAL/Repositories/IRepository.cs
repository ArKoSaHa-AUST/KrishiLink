using System.Linq.Expressions;

namespace KrishiLink.DAL.Repositories
{
    /// <summary>
    /// Generic data-access contract used by the BLL. <see cref="Query"/> exposes a composable, no-tracking
    /// query so services can filter/project without pulling whole tables into memory.
    /// </summary>
    public interface IRepository<T> where T : class
    {
        IQueryable<T> Query();
        IQueryable<T> QueryTracked();
        Task<T?> GetByIdAsync(int id);
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        Task<int> SaveChangesAsync();
    }
}
