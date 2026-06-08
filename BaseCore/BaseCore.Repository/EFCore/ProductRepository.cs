using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;

namespace BaseCore.Repository.EFCore
{
    /// <summary>
    /// Product Repository using Entity Framework Core
    /// </summary>
    public interface IProductRepositoryEF : IRepository<Product>
    {
        Task<(List<Product> Products, int TotalCount)> SearchAsync(
            string? keyword, int? categoryId, int page, int pageSize,
            decimal? minPrice = null, decimal? maxPrice = null,
            bool? onlyNew = null, bool? onlySale = null,
            string? sort = null, bool isActive = true);
        Task<List<Product>> GetByCategoryAsync(int categoryId);
    }

    public class ProductRepositoryEF : Repository<Product>, IProductRepositoryEF
    {
        public ProductRepositoryEF(MySqlDbContext context) : base(context)
        {
        }

        public async Task<(List<Product> Products, int TotalCount)> SearchAsync(
            string? keyword, int? categoryId, int page, int pageSize,
            decimal? minPrice = null, decimal? maxPrice = null,
            bool? onlyNew = null, bool? onlySale = null,
            string? sort = null, bool isActive = true)
        {
            var query = _dbSet.Include(p => p.Category).AsQueryable();

            if (isActive)
                query = query.Where(p => p.IsActive);

            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(keyword) ||
                    (p.Description != null && p.Description.ToLower().Contains(keyword)));
            }

            if (categoryId.HasValue && categoryId > 0)
                query = query.Where(p => p.CategoryId == categoryId);

            if (minPrice.HasValue && minPrice.Value > 0)
                query = query.Where(p => (p.DiscountPrice ?? p.Price) >= minPrice.Value);

            if (maxPrice.HasValue && maxPrice.Value > 0)
                query = query.Where(p => (p.DiscountPrice ?? p.Price) <= maxPrice.Value);

            if (onlyNew.HasValue && onlyNew.Value)
                query = query.Where(p => p.IsNew);

            if (onlySale.HasValue && onlySale.Value)
                query = query.Where(p => p.DiscountPrice != null);

            var totalCount = await query.CountAsync();

            IOrderedQueryable<Product> ordered = sort switch
            {
                "price_asc"  => query.OrderBy(p => p.DiscountPrice ?? p.Price),
                "price_desc" => query.OrderByDescending(p => p.DiscountPrice ?? p.Price),
                "name"       => query.OrderBy(p => p.Name),
                "bestseller" => query.OrderByDescending(p => p.SoldCount),
                _            => query.OrderByDescending(p => p.Id)
            };

            var products = await ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (products, totalCount);
        }

        public async Task<List<Product>> GetByCategoryAsync(int categoryId)
        {
            return await _dbSet
                .Where(p => p.CategoryId == categoryId)
                .Include(p => p.Category)
                .ToListAsync();
        }
    }
}
