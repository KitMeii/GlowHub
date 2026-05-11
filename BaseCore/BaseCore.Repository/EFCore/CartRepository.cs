using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;

namespace BaseCore.Repository.EFCore
{
    public interface ICartRepositoryEF : IRepository<CartItem>
    {
        Task<List<CartItem>> GetByUserAsync(string userId);
        Task<CartItem?> GetByUserAndProductAsync(string userId, int productId);
        Task ClearByUserAsync(string userId);
    }

    public class CartRepositoryEF : Repository<CartItem>, ICartRepositoryEF
    {
        public CartRepositoryEF(MySqlDbContext context) : base(context) { }

        public async Task<List<CartItem>> GetByUserAsync(string userId)
        {
            return await _dbSet
                .Where(c => c.UserId == userId)
                .Include(c => c.Product)
                    .ThenInclude(p => p.Category)
                .OrderBy(c => c.AddedAt)
                .ToListAsync();
        }

        public async Task<CartItem?> GetByUserAndProductAsync(string userId, int productId)
        {
            return await _dbSet
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
        }

        public async Task ClearByUserAsync(string userId)
        {
            var items = await _dbSet.Where(c => c.UserId == userId).ToListAsync();
            _dbSet.RemoveRange(items);
            await _context.SaveChangesAsync();
        }
    }
}