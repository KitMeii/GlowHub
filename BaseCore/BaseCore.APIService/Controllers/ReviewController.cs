using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;
using BaseCore.Repository;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly MySqlDbContext _context;
        public ReviewsController(MySqlDbContext context) => _context = context;

        /// <summary>Admin xem tất cả review</summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var reviews = await (from r in _context.Reviews
                                 join p in _context.Products on r.ProductId equals p.Id
                                 join u in _context.Users on r.UserId equals u.Id
                                 orderby r.CreatedAt descending
                                 select new
                                 {
                                     r.Id,
                                     ProductId = p.Id,
                                     ProductName = p.Name,
                                     UserId = u.Id,
                                     UserName = u.Name,
                                     r.Rating,
                                     r.Comment,
                                     r.CreatedAt
                                 }).ToListAsync();
            return Ok(reviews);
        }

        /// <summary>Admin xóa 1 review</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();
            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }

    /// <summary>Review của 1 sản phẩm — public list + user post (chỉ khi đã COMPLETED đơn chứa product).</summary>
    [Route("api/products/{productId:int}/reviews")]
    [ApiController]
    public class ProductReviewsController : ControllerBase
    {
        private readonly MySqlDbContext _context;
        public ProductReviewsController(MySqlDbContext context) => _context = context;

        /// <summary>Public: list review của 1 sản phẩm, kèm tên user.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetByProduct(int productId)
        {
            var reviews = await (from r in _context.Reviews
                                 join u in _context.Users on r.UserId equals u.Id
                                 where r.ProductId == productId
                                 orderby r.CreatedAt descending
                                 select new
                                 {
                                     r.Id,
                                     r.ProductId,
                                     r.Rating,
                                     r.Comment,
                                     r.CreatedAt,
                                     UserId = u.Id,
                                     UserName = u.Name ?? u.UserName
                                 }).ToListAsync();

            var avgRating = reviews.Count == 0 ? 0 : reviews.Average(r => r.Rating);
            return Ok(new
            {
                items = reviews,
                totalCount = reviews.Count,
                avgRating = Math.Round(avgRating, 1)
            });
        }

        /// <summary>User post review — chỉ khi đã có đơn COMPLETED chứa product. 1 user × 1 product = 1 review (upsert).</summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Post(int productId, [FromBody] ReviewDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { message = "Rating phải từ 1 đến 5." });

            // Kiểm tra user đã mua + nhận sản phẩm này chưa
            var hasCompletedOrder = await _context.Orders
                .Where(o => o.UserId == userId && o.Status == OrderStatus.Completed)
                .AnyAsync(o => o.OrderDetails.Any(od => od.ProductId == productId));

            if (!hasCompletedOrder)
                return StatusCode(403, new { message = "Bạn cần mua và nhận sản phẩm này trước khi đánh giá." });

            // Upsert: nếu user đã review product này → update; nếu chưa → tạo mới
            var existing = await _context.Reviews
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId);

            if (existing != null)
            {
                existing.Rating = dto.Rating;
                existing.Comment = dto.Comment ?? "";
                existing.CreatedAt = DateTime.Now;
            }
            else
            {
                _context.Reviews.Add(new Review
                {
                    ProductId = productId,
                    UserId = userId,
                    Rating = dto.Rating,
                    Comment = dto.Comment ?? "",
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cảm ơn bạn đã đánh giá!" });
        }
    }

    public class ReviewDto
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}
