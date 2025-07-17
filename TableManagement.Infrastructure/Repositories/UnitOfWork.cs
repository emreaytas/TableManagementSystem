using Microsoft.EntityFrameworkCore.Storage;
using TableManagement.Core.Interfaces;
using TableManagement.Infrastructure.Repositories;

namespace TableManagement.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly Dictionary<Type, object> _repositories;
        private IDbContextTransaction? _transaction;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
            _repositories = new Dictionary<Type, object>();
        }

        public ICustomTableRepository CustomTables =>
            GetRepository<ICustomTableRepository>(() => new CustomTableRepository(_context));

        public ICustomTableDataRepository CustomTableData =>
            GetRepository<ICustomTableDataRepository>(() => new CustomTableDataRepository(_context));

        public IRepository<T> Repository<T>() where T : class
        {
            return GetRepository<IRepository<T>>(() => new Repository<T>(_context));
        }

        private TRepo GetRepository<TRepo>(Func<TRepo> factory) where TRepo : class
        {
            var type = typeof(TRepo);
            if (!_repositories.ContainsKey(type))
            {
                _repositories[type] = factory();
            }
            return (TRepo)_repositories[type];
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
            return _transaction;
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }
}