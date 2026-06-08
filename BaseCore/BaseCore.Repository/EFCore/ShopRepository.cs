using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;

namespace BaseCore.Repository.EFCore
{
    public interface IShopRepositoryEF : IRepository<Shop>
    {
        Task<Shop?> GetBySellerIdAsync(string sellerId);
        Task<(List<Shop> Shops, int TotalCount)> GetAllPagedAsync(int page, int pageSize);
        Task<Shop?> GetWithProductsAsync(string shopId);
        Task AddProductAsync(ShopProduct shopProduct);
        Task RemoveProductAsync(string shopId, int productId);
    }

    public class ShopRepositoryEF : Repository<Shop>, IShopRepositoryEF
    {
        public ShopRepositoryEF(MySqlDbContext context) : base(context)
        {
        }

        public async Task<Shop?> GetBySellerIdAsync(string sellerId)
        {
            return await _dbSet
                .Include(s => s.Seller)
                .FirstOrDefaultAsync(s => s.SellerId == sellerId);
        }

        public async Task<(List<Shop> Shops, int TotalCount)> GetAllPagedAsync(int page, int pageSize)
        {
            var query = _dbSet.Include(s => s.Seller).AsQueryable();
            var total = await query.CountAsync();
            var shops = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return (shops, total);
        }

        public async Task<Shop?> GetWithProductsAsync(string shopId)
        {
            return await _dbSet
                .Include(s => s.Seller)
                .Include(s => s.ShopProducts)
                    .ThenInclude(sp => sp.Product)
                .FirstOrDefaultAsync(s => s.Id == shopId);
        }

        public async Task AddProductAsync(ShopProduct shopProduct)
        {
            await _context.Set<ShopProduct>().AddAsync(shopProduct);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveProductAsync(string shopId, int productId)
        {
            var sp = await _context.Set<ShopProduct>()
                .FirstOrDefaultAsync(x => x.ShopId == shopId && x.ProductId == productId);
            if (sp != null)
            {
                _context.Set<ShopProduct>().Remove(sp);
                await _context.SaveChangesAsync();
            }
        }
    }
}
