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
            return await _context.CustomTables
                .Include(t => t.Columns.OrderBy(c => c.DisplayOrder))
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<CustomTable?> GetUserTableByIdAsync(int tableId, int userId)
        {
            return await _context.CustomTables
                .Include(t => t.Columns.OrderBy(c => c.DisplayOrder))
                .FirstOrDefaultAsync(t => t.Id == tableId && t.UserId == userId);
        }

        public async Task<bool> TableNameExistsForUserAsync(string tableName, int userId)
        {
            return await _context.CustomTables
                .AnyAsync(t => t.TableName == tableName && t.UserId == userId);
        }

        public async Task<IEnumerable<CustomTable>> GetUserTablesWithColumnsAsync(int userId)
        {
            return await _context.CustomTables
                .Include(t => t.Columns.OrderBy(c => c.DisplayOrder))
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<CustomTable?> GetUserTableWithColumnsAsync(int tableId, int userId)
        {
            return await _context.CustomTables
                .Include(t => t.Columns.OrderBy(c => c.DisplayOrder))
                .FirstOrDefaultAsync(t => t.Id == tableId && t.UserId == userId);
        }
    }
}