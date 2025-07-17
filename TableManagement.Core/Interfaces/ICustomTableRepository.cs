using TableManagement.Core.Entities;

namespace TableManagement.Core.Interfaces
{
    public interface ICustomTableRepository : IRepository<CustomTable>
    {
        Task<IEnumerable<CustomTable>> GetUserTablesAsync(int userId);
        Task<CustomTable?> GetUserTableByIdAsync(int tableId, int userId);
        Task<bool> TableNameExistsForUserAsync(string tableName, int userId);
        Task<IEnumerable<CustomTable>> GetUserTablesWithColumnsAsync(int userId);
        Task<CustomTable?> GetUserTableWithColumnsAsync(int tableId, int userId);
    }
}