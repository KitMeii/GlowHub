using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/recently-viewed")]
    [ApiController]
    [Authorize]
    public class RecentlyViewedController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public RecentlyViewedController(MySqlDbContext db) => _db = db;

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        /// <summary>GET /api/recently-viewed — Lấy SP đã xem gần đây (tối đa 10)</summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var items = await _db.RecentlyVieweds
                .Include(r => r.Product).ThenInclude(p => p.Category)
                .Where(r => r.UserId == UserId && r.Product.IsActive)
                .OrderByDescending(r => r.ViewedAt)
                .Take(10)
                .Select(r => new
                {
                    productId = r.ProductId,
                    viewedAt = r.ViewedAt,
                    name = r.Product.Name,
                    image = r.Product.ImageUrl,
                    price = r.Product.Price,
                    discountPrice = r.Product.DiscountPrice,
                    category = r.Product.Category != null ? r.Product.Category.Name : null
                })
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>POST /api/recently-viewed/{productId} — Thêm vào lịch sử xem</summary>
        [HttpPost("{productId:int}")]
        public async Task<IActionResult> Add(int productId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound();

            var existing = await _db.RecentlyVieweds
                .FirstOrDefaultAsync(r => r.UserId == UserId && r.ProductId == productId);

            if (existing != null)
            {
                existing.ViewedAt = DateTime.UtcNow;
            }
            else
            {
                // Giữ tối đa 20 bản ghi, xóa cũ nhất nếu vượt quá
                var count = await _db.RecentlyVieweds.CountAsync(r => r.UserId == UserId);
                if (count >= 20)
                {
                    var oldest = await _db.RecentlyVieweds
                        .Where(r => r.UserId == UserId)
                        .OrderBy(r => r.ViewedAt)
                        .FirstAsync();
                    _db.RecentlyVieweds.Remove(oldest);
                }

                _db.RecentlyVieweds.Add(new RecentlyViewed
                {
                    UserId = UserId,
                    ProductId = productId,
                    ViewedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
