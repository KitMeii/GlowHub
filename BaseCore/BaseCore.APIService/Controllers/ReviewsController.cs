using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [ApiController]
    [Route("api/products/{productId}/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        public ReviewsController(MySqlDbContext db) => _db = db;

        // GET /api/products/{productId}/reviews
        [HttpGet]
        public async Task<IActionResult> GetReviews(int productId)
        {
            var reviews = await _db.Reviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new {
                    r.Id,
                    r.ProductId,
                    r.UserId,
                    UserName = r.UserId,   // frontend hiển thị UserId tạm
                    r.Rating,
                    r.Comment,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(reviews);
        }

        // POST /api/products/{productId}/reviews  — cần đăng nhập
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddReview(int productId, [FromBody] ReviewDto dto)
        {
            var product = await _db.Products.FindAsync(productId);
            if (product == null)
                return NotFound(new { message = "Không tìm thấy sản phẩm" });

            // Lấy UserId từ JWT Claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? "guest";

            // Nếu đã review rồi → cập nhật
            var existing = await _db.Reviews
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId);

            if (existing != null)
            {
                existing.Rating = Math.Clamp(dto.Rating, 1, 5);
                existing.Comment = dto.Comment ?? existing.Comment;
                existing.CreatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                return Ok(existing);
            }

            // Tạo review mới
            var review = new Review
            {
                ProductId = productId,
                UserId = userId,
                Rating = Math.Clamp(dto.Rating, 1, 5),
                Comment = dto.Comment ?? "",
                CreatedAt = DateTime.Now,
            };
            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            return Ok(review);
        }
    }

    public class ReviewDto
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}