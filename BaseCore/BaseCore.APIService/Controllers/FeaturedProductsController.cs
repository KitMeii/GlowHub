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
        public async Task<IActionResult> GetBySection([FromQuery] string section = "new_arrivals")
        {
            var items = await _db.FeaturedProducts
                .Include(fp => fp.Product)
                    .ThenInclude(p => p!.Category)
                .Where(fp => fp.Section == section && fp.IsActive && fp.Product!.IsActive)
                .OrderBy(fp => fp.SortOrder)
                .Select(fp => new {
                    fp.Id,
                    fp.SortOrder,
                    Product = new
                    {
                        fp.Product!.Id,
                        fp.Product.Name,
                        fp.Product.Price,
                        fp.Product.DiscountPrice,
                        fp.Product.ImageUrl,
                        fp.Product.Description,
                        fp.Product.Stock,
                        fp.Product.IsNew,
                        Category = fp.Product.Category != null ? fp.Product.Category.Name : ""
                    }
                })
                .ToListAsync();

            return Ok(items);
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