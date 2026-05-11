using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        public CategoriesController(MySqlDbContext db) => _db = db;

        // GET /api/categories
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var cats = await _db.Categories
                .Select(c => new {
                    c.Id,
                    c.Name,
                    c.Description,
                    ProductCount = _db.Products
                        .Count(p => p.CategoryId == c.Id)
                })
                .OrderBy(c => c.Name)
                .ToListAsync();
            return Ok(cats);
        }

        // GET /api/categories/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound(new { message = "Không tìm thấy danh mục" });
            return Ok(cat);
        }
    }
}