using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;
using BaseCore.Repository;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/wishlist")]
    [ApiController]
    [Authorize]
    public class WishlistController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public WishlistController(MySqlDbContext db)
        {
            _db = db;
        }

        private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
                                   ?? User.FindFirstValue("sub") ?? "";

        // GET /api/wishlist
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();
            var items = await _db.Wishlists
                .Include(w => w.Product).ThenInclude(p => p!.Category)
                .Where(w => w.CustomerId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new {
                    wishlistId   = w.Id,
                    productId    = w.ProductId,
                    name         = w.Product != null ? w.Product.Name : "",
                    price        = w.Product != null ? w.Product.Price : 0m,
                    discountPrice = w.Product != null ? w.Product.DiscountPrice : null,
                    image        = w.Product != null ? w.Product.ImageUrl : "",
                    isActive     = w.Product != null && w.Product.IsActive,
                    category     = w.Product != null && w.Product.Category != null ? w.Product.Category.Name : "",
                    addedAt      = w.CreatedAt
                })
                .ToListAsync();

            return Ok(items);
        }

        // POST /api/wishlist/{productId}
        [HttpPost("{productId:int}")]
        public async Task<IActionResult> Add(int productId)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound(new { message = "Sản phẩm không tồn tại" });

            var userId = GetUserId();
            var exists = await _db.Wishlists
                .AnyAsync(w => w.CustomerId == userId && w.ProductId == productId);

            if (exists) return Ok(new { message = "Sản phẩm đã có trong danh sách yêu thích" });

            var item = new Wishlist { CustomerId = userId, ProductId = productId };
            _db.Wishlists.Add(item);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã thêm vào danh sách yêu thích", id = item.Id });
        }

        // DELETE /api/wishlist/{productId}
        [HttpDelete("{productId:int}")]
        public async Task<IActionResult> Remove(int productId)
        {
            var userId = GetUserId();
            var item = await _db.Wishlists
                .FirstOrDefaultAsync(w => w.CustomerId == userId && w.ProductId == productId);

            if (item == null) return NotFound(new { message = "Không tìm thấy trong danh sách yêu thích" });

            _db.Wishlists.Remove(item);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã xóa khỏi danh sách yêu thích" });
        }

        // GET /api/wishlist/check/{productId}
        [HttpGet("check/{productId:int}")]
        public async Task<IActionResult> Check(int productId)
        {
            var userId = GetUserId();
            var inWishlist = await _db.Wishlists
                .AnyAsync(w => w.CustomerId == userId && w.ProductId == productId);

            return Ok(new { inWishlist });
        }
    }
}
