using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeaturedProductsController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public FeaturedProductsController(MySqlDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// GET /api/FeaturedProducts?section=new_arrivals
        /// Trả về sản phẩm nổi bật kèm thông tin Product đầy đủ
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBySection([FromQuery] string? section = null)
        {
            try
            {
                // Nếu không truyền section → lấy tất cả; ngược lại lọc theo section
                var query = _db.FeaturedProducts.Where(fp => fp.IsActive);
                if (!string.IsNullOrWhiteSpace(section))
                    query = query.Where(fp => fp.Section == section);

                var featuredList = await query
                    .OrderBy(fp => fp.Section)
                    .ThenBy(fp => fp.SortOrder)
                    .ToListAsync();

                // Lấy danh sách ProductId để query 1 lần
                var productIds = featuredList.Select(fp => fp.ProductId).Distinct().ToList();

                // Load products + categories trong 1 query
                var products = await _db.Products
                    .Where(p => productIds.Contains(p.Id) && p.IsActive)
                    .ToDictionaryAsync(p => p.Id);

                var categoryIds = products.Values
                    .Select(p => p.CategoryId)
                    .Distinct()
                    .ToList();

                var categories = await _db.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);

                // Build response thủ công — tránh lỗi EF Core translation với [NotMapped]
                var result = new List<object>();
                foreach (var fp in featuredList)
                {
                    if (!products.TryGetValue(fp.ProductId, out var product))
                        continue;

                    categories.TryGetValue(product.CategoryId, out var category);

                    result.Add(new
                    {
                        fp.Id,
                        fp.Section,
                        fp.SortOrder,
                        Product = new
                        {
                            product.Id,
                            product.Name,
                            Price = product.Price,
                            product.ImageUrl,
                            product.Description,
                            product.Stock,
                            Category = category?.Name ?? ""
                        }
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>POST /api/FeaturedProducts — Thêm sản phẩm vào section (admin only)</summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Add([FromBody] FeaturedProductDto dto)
        {
            // Kiểm tra đã tồn tại chưa
            var exists = await _db.FeaturedProducts
                .AnyAsync(fp => fp.ProductId == dto.ProductId && fp.Section == dto.Section);
            if (exists)
                return Conflict(new { message = "Sản phẩm đã có trong section này" });

            // Kiểm tra product tồn tại
            var product = await _db.Products.FindAsync(dto.ProductId);
            if (product == null)
                return NotFound(new { message = "Sản phẩm không tồn tại" });

            // SortOrder tự động nếu không chỉ định
            if (dto.SortOrder == 0)
            {
                dto.SortOrder = (await _db.FeaturedProducts
                    .Where(fp => fp.Section == dto.Section)
                    .MaxAsync(fp => (int?)fp.SortOrder) ?? 0) + 1;
            }

            var featured = new FeaturedProduct
            {
                ProductId = dto.ProductId,
                Section = dto.Section,
                SortOrder = dto.SortOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.FeaturedProducts.Add(featured);
            await _db.SaveChangesAsync();
            return Ok(featured);
        }

        /// <summary>DELETE /api/FeaturedProducts/{id} (admin only)</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Remove(int id)
        {
            var item = await _db.FeaturedProducts.FindAsync(id);
            if (item == null) return NotFound();
            _db.FeaturedProducts.Remove(item);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa khỏi danh sách nổi bật" });
        }

        /// <summary>PUT /api/FeaturedProducts/reorder — Sắp xếp lại (admin only)</summary>
        [HttpPut("reorder")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reorder([FromBody] List<ReorderDto> items)
        {
            foreach (var item in items)
            {
                var fp = await _db.FeaturedProducts.FindAsync(item.Id);
                if (fp != null) fp.SortOrder = item.SortOrder;
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật thứ tự" });
        }
    }

    public class FeaturedProductDto
    {
        public int ProductId { get; set; }
        public string Section { get; set; } = "new_arrivals";
        public int SortOrder { get; set; } = 0;
    }

    public class ReorderDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }
}