using MongoDB.Driver;
using BaseCore.Entities;
using BaseCore.Repository;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BaseCore.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly MongoDbContext _context;

        public CategoryService(MongoDbContext context)
        {
            _context = context;
        }

        public async Task<List<Category>> GetAllAsync()
        {
            return await _context.Categories.Find(_ => true).ToListAsync();
        }

        public async Task<Category> GetByIdAsync(int id)
        {
            return await _context.Categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Category> CreateAsync(Category category)
        {
            // Get next ID
            var maxCategory = await _context.Categories
                .Find(_ => true)
                .SortByDescending(c => c.Id)
                .FirstOrDefaultAsync();
            category.Id = (maxCategory?.Id ?? 0) + 1;

            await _context.Categories.InsertOneAsync(category);
            return category;
        }

        public async Task UpdateAsync(Category category)
        {
            await _context.Categories.ReplaceOneAsync(c => c.Id == category.Id, category);
        }

        public async Task DeleteAsync(int id)
        {
            await _context.Categories.DeleteOneAsync(c => c.Id == id);
        }
    }
}
