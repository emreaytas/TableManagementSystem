using Microsoft.EntityFrameworkCore;
using TableManagement.Core.Entities;
using TableManagement.Core.Interfaces;
using TableManagement.Infrastructure.Data;

namespace TableManagement.Infrastructure.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User> GetByUserNameAsync(string userName)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.UserName == userName);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _dbSet.AnyAsync(u => u.Email == email);
        }

        public async Task<bool> UserNameExistsAsync(string userName)
        {
            return await _dbSet.AnyAsync(u => u.UserName == userName);
        }
    }
}
