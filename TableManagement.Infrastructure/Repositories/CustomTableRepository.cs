using Microsoft.EntityFrameworkCore;
using TableManagement.Core.Entities;
using TableManagement.Core.Interfaces;
using TableManagement.Infrastructure.Data;

namespace TableManagement.Infrastructure.Repositories
{
    public class CustomTableRepository : Repository<CustomTable>, ICustomTableRepository
    {
        public CustomTableRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<CustomTable>> GetUserTablesAsync(int userId)
        {
            return await _dbSet
                .Where(t => t.UserId == userId)
                .Include(t => t.Columns)
                .ToListAsync();
        }

        public async Task<CustomTable> GetTableWithColumnsAsync(int tableId)
        {
            return await _dbSet
                .Include(t => t.Columns)
                .FirstOrDefaultAsync(t => t.Id == tableId);
        }

        public async Task<CustomTable> GetTableWithDataAsync(int tableId)
        {
            return await _dbSet
                .Include(t => t.Columns)
                .Include(t => t.TableData)
                .ThenInclude(td => td.Column)
                .FirstOrDefaultAsync(t => t.Id == tableId);
        }

        public async Task<bool> TableNameExistsForUserAsync(string tableName, int userId)
        {
            return await _dbSet.AnyAsync(t => t.TableName == tableName && t.UserId == userId);
        }
    }
}