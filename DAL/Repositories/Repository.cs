using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.DAL.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly ApplicationDbContext _db;
        private readonly DbSet<T> _set;

        public Repository(ApplicationDbContext db)
        {
            _db = db;
            _set = db.Set<T>();
        }

        public IQueryable<T> Query() => _set.AsNoTracking();

        public IQueryable<T> QueryTracked() => _set;

        public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) => _set.FirstOrDefaultAsync(predicate);

        public async Task AddAsync(T entity) => await _set.AddAsync(entity);

        public void Update(T entity) => _set.Update(entity);

        public void Remove(T entity) => _set.Remove(entity);

        public void RemoveRange(IEnumerable<T> entities) => _set.RemoveRange(entities);

        public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();
    }
}
