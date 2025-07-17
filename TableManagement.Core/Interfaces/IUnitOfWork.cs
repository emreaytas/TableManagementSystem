using Microsoft.EntityFrameworkCore.Storage;

namespace TableManagement.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        ICustomTableRepository CustomTables { get; }
        ICustomTableDataRepository CustomTableData { get; }
        IRepository<T> Repository<T>() where T : class;

        Task<int> SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}