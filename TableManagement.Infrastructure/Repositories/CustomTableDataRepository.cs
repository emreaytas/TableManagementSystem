using Microsoft.EntityFrameworkCore;
using TableManagement.Core.Entities;
using TableManagement.Core.Interfaces;
using TableManagement.Infrastructure.Data;

namespace TableManagement.Infrastructure.Repositories
{
    public class CustomTableDataRepository : Repository<CustomTableData>, ICustomTableDataRepository
    {
        public CustomTableDataRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<CustomTableData>> GetTableDataAsync(int tableId)
        {
            return await _dbSet
                .Include(td => td.Column)
                .Where(td => td.CustomTableId == tableId)
                .OrderBy(td => td.RowIdentifier)
                .ThenBy(td => td.Column.DisplayOrder)
                .ToListAsync();
        }

        public async Task<IEnumerable<CustomTableData>> GetRowDataAsync(int tableId, int rowIdentifier)
        {
            return await _dbSet
                .Include(td => td.Column)
                .Where(td => td.CustomTableId == tableId && td.RowIdentifier == rowIdentifier)
                .OrderBy(td => td.Column.DisplayOrder)
                .ToListAsync();
        }

        public async Task DeleteRowDataAsync(int tableId, int rowIdentifier)
        {
            var rowData = await _dbSet
                .Where(td => td.CustomTableId == tableId && td.RowIdentifier == rowIdentifier)
                .ToListAsync();

            _dbSet.RemoveRange(rowData);
        }

        public async Task<int> GetNextRowIdentifierAsync(int tableId)
        {
            var maxRowId = await _dbSet
                .Where(td => td.CustomTableId == tableId)
                .MaxAsync(td => (int?)td.RowIdentifier) ?? 0;

            return maxRowId + 1;
        }
    }
}