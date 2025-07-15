using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TableManagement.Core.Entities;

namespace TableManagement.Core.Interfaces
{
    public interface ICustomTableDataRepository : IRepository<CustomTableData>
    {
        Task<IEnumerable<CustomTableData>> GetTableDataAsync(int tableId);
        Task<IEnumerable<CustomTableData>> GetRowDataAsync(int tableId, int rowIdentifier);
        Task DeleteRowDataAsync(int tableId, int rowIdentifier);
        Task<int> GetNextRowIdentifierAsync(int tableId);
    }



}
