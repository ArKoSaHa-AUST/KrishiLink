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
        void Detach(T entity);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        Task<int> SaveChangesAsync();
        Task<WorkflowTransaction> BeginWorkflowAsync(CancellationToken ct = default);

        /// <summary>Begins a workflow that contends only for <paramref name="locks"/> when sharded locking is enabled.</summary>
        Task<WorkflowTransaction> BeginWorkflowAsync(IEnumerable<WorkflowLock> locks, CancellationToken ct = default);
    }
}
