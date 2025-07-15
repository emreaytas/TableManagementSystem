using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TableManagement.Core.Entities;

namespace TableManagement.Core.Interfaces
{
    public interface ICustomTableRepository : IRepository<CustomTable>
    {
        Task<IEnumerable<CustomTable>> GetUserTablesAsync(int userId);
        Task<CustomTable> GetTableWithColumnsAsync(int tableId);
        Task<CustomTable> GetTableWithDataAsync(int tableId);
        Task<bool> TableNameExistsForUserAsync(string tableName, int userId);
    }

}
