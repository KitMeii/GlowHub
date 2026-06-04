using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
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
                .Where(c => !c.IsDeleted)
                .Select(c => new {
                    c.Id,
                    c.Name,
                    c.Description,
                    ProductCount = _db.Products.Count(p => p.CategoryId == c.Id)
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

        // POST /api/categories (Admin)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CategoryDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Tên danh mục là bắt buộc" });

            var exists = await _db.Categories.AnyAsync(c => c.Name == dto.Name.Trim());
            if (exists) return Conflict(new { message = "Danh mục đã tồn tại" });

            var cat = new Category { Name = dto.Name.Trim(), Description = dto.Description };
            _db.Categories.Add(cat);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã tạo danh mục", category = cat });
        }

        // PUT /api/categories/{id} (Admin)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryDto dto)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return NotFound(new { message = "Không tìm thấy danh mục" });

            if (!string.IsNullOrWhiteSpace(dto.Name)) cat.Name = dto.Name.Trim();
            if (dto.Description != null) cat.Description = dto.Description;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật danh mục", category = cat });
        }

        // DELETE /api/categories/{id} (Admin) — soft delete
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null || cat.IsDeleted) return NotFound(new { message = "Không tìm thấy danh mục" });

            cat.IsDeleted = true;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa danh mục" });
        }
    }

    public class CategoryDto
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }
}