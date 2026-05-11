using MongoDB.Driver;
using BaseCore.Entities;
using BaseCore.Repository;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BaseCore.Services
{
    public class ProductService : IProductService
    {
        private readonly MongoDbContext _context;

        public ProductService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            var products = await _context.Products.Find(_ => true).ToListAsync();

            // Load categories for each product
            foreach (var product in products)
            {
                product.Category = await _context.Categories
                    .Find(c => c.Id == product.CategoryId)
                    .FirstOrDefaultAsync();
            }

            return products;
        }

        public async Task<Product> GetProductByIdAsync(int id)
        {
            var product = await _context.Products
                .Find(p => p.Id == id)
                .FirstOrDefaultAsync();

            if (product != null)
            {
                product.Category = await _context.Categories
                    .Find(c => c.Id == product.CategoryId)
                    .FirstOrDefaultAsync();
            }

            return product;
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            // Get next ID
            var maxProduct = await _context.Products
                .Find(_ => true)
                .SortByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            product.Id = (maxProduct?.Id ?? 0) + 1;

            await _context.Products.InsertOneAsync(product);
            return product;
        }

        public async Task UpdateProductAsync(Product product)
        {
            await _context.Products.ReplaceOneAsync(p => p.Id == product.Id, product);
        }

        public async Task DeleteProductAsync(int id)
        {
            await _context.Products.DeleteOneAsync(p => p.Id == id);
        }

        public async Task<(List<Product> Products, int TotalCount)> SearchAsync(string keyword, int? categoryId, int page, int pageSize)
        {
            var filterBuilder = Builders<Product>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrEmpty(keyword))
            {
                var keywordFilter = filterBuilder.Or(
                    filterBuilder.Regex(p => p.Name, new MongoDB.Bson.BsonRegularExpression(keyword, "i")),
                    filterBuilder.Regex(p => p.Description, new MongoDB.Bson.BsonRegularExpression(keyword, "i"))
                );
                filter = filterBuilder.And(filter, keywordFilter);
            }

            if (categoryId.HasValue)
            {
                filter = filterBuilder.And(filter, filterBuilder.Eq(p => p.CategoryId, categoryId.Value));
            }

            var totalCount = (int)await _context.Products.CountDocumentsAsync(filter);

            var products = await _context.Products
                .Find(filter)
                .SortByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            // Load categories
            foreach (var product in products)
            {
                product.Category = await _context.Categories
                    .Find(c => c.Id == product.CategoryId)
                    .FirstOrDefaultAsync();
            }

            return (products, totalCount);
        }
    }
}
