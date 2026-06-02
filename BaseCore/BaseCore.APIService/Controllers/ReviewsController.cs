using BaseCore.Common;
using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Repository.EFCore;
using BaseCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    // ─── Customer: GET + POST per product ───────────────────────────────────────
    [ApiController]
    [Route("api/products/{productId}/reviews")]
    public class ReviewsController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly NotificationService _notificationService;

        public ReviewsController(MySqlDbContext db, NotificationService notificationService)
        {
            _db                  = db;
            _notificationService = notificationService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
                                    ?? User.FindFirstValue("sub");

        // GET /api/products/{productId}/reviews
        [HttpGet]
        public async Task<IActionResult> GetReviews(int productId)
        {
            var rows = await _db.Reviews
                .Include(r => r.User)
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var reviews = rows.Select(r => new {
                r.Id, r.ProductId, r.UserId,
                UserName  = r.User != null ? (r.User.Name ?? r.User.UserName) : r.UserId,
                r.Rating, r.Comment,
                images = r.Images != null
                    ? r.Images.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    : new List<string>(),
                r.IsVerifiedPurchase,
                r.CreatedAt,
                r.SellerReply, r.ReplyAt
            });

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

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? "guest";

            var existing = await _db.Reviews
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId);

            if (existing != null)
            {
                existing.Rating    = Math.Clamp(dto.Rating, 1, 5);
                existing.Comment   = dto.Comment ?? existing.Comment;
                existing.Images    = dto.Images != null ? string.Join(",", dto.Images.Take(5)) : existing.Images;
                existing.CreatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                return Ok(existing);
            }

            var review = new Review
            {
                ProductId = productId,
                UserId    = userId,
                Rating    = Math.Clamp(dto.Rating, 1, 5),
                Comment   = dto.Comment ?? "",
                Images    = dto.Images != null ? string.Join(",", dto.Images.Take(5)) : null,
                CreatedAt = DateTime.Now,
            };
            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            // Notify seller when their product gets a new review
            if (product.ShopId != null)
            {
                var shop = await _db.Shops.FindAsync(product.ShopId);
                if (shop != null)
                {
                    await _notificationService.CreateAsync(
                        shop.SellerId,
                        NotificationType.NewReview,
                        "Đánh giá mới",
                        $"Sản phẩm \"{product.Name}\" nhận đánh giá {review.Rating} sao",
                        "seller-dashboard.html#reviews"
                    );
                }
            }

            return Ok(review);
        }

        // PUT /api/products/{productId}/reviews/{reviewId}  — chỉ tác giả mới được sửa
        [HttpPut("{reviewId:int}")]
        [Authorize]
        public async Task<IActionResult> UpdateReview(int productId, int reviewId, [FromBody] UpdateReviewDto dto)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();
            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { message = "Rating phải từ 1 đến 5" });

            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId && r.UserId == userId);
            if (review == null)
                return NotFound(new { message = "Đánh giá không tồn tại hoặc không thuộc về bạn" });

            review.Rating    = dto.Rating;
            if (dto.Comment != null) review.Comment = dto.Comment;
            review.CreatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật đánh giá" });
        }

        // DELETE /api/products/{productId}/reviews/{reviewId}  — chỉ tác giả mới được xóa
        [HttpDelete("{reviewId:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(int productId, int reviewId)
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized();

            var review = await _db.Reviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.ProductId == productId && r.UserId == userId);
            if (review == null)
                return NotFound(new { message = "Đánh giá không tồn tại hoặc không thuộc về bạn" });

            _db.Reviews.Remove(review);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa đánh giá" });
        }
    }

    // ─── Seller: Xem + Phản hồi review ──────────────────────────────────────────
    [Route("api/reviews")]
    [ApiController]
    [Authorize(Roles = RoleConstant.Seller)]
    public class SellerReviewsController : ControllerBase
    {
        private readonly IShopRepositoryEF _shopRepository;
        private readonly MySqlDbContext _db;

        public SellerReviewsController(IShopRepositoryEF shopRepository, MySqlDbContext db)
        {
            _shopRepository = shopRepository;
            _db             = db;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<(Shop? shop, IActionResult? error)> GetActiveShopAsync()
        {
            var shop = await _shopRepository.GetBySellerIdAsync(GetUserId()!);
            if (shop == null) return (null, NotFound(new { message = "Bạn chưa có shop" }));
            if (shop.Status != ShopStatus.Active) return (null, BadRequest(new { message = "Shop chưa được duyệt" }));
            return (shop, null);
        }

        private async Task<List<int>> GetShopProductIdsAsync(string shopId)
            => await _db.Products.Where(p => p.ShopId == shopId).Select(p => p.Id).ToListAsync();

        // GET /api/reviews/shop?page=1&limit=10&rating=5&replied=false
        [HttpGet("shop")]
        public async Task<IActionResult> GetShopReviews(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            [FromQuery] int? rating = null,
            [FromQuery] bool? replied = null)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await GetShopProductIdsAsync(shop!.Id);

            var query = _db.Reviews.Include(r => r.Product)
                .Where(r => productIds.Contains(r.ProductId));

            if (rating.HasValue)  query = query.Where(r => r.Rating == rating.Value);
            if (replied == true)  query = query.Where(r => r.SellerReply != null);
            if (replied == false) query = query.Where(r => r.SellerReply == null);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(r => new {
                    reviewId     = r.Id,
                    productName  = r.Product != null ? r.Product.Name     : "",
                    productImage = r.Product != null ? r.Product.ImageUrl : "",
                    customerId   = r.UserId,
                    customerName = r.UserId,
                    r.Rating,
                    r.Comment,
                    images              = r.Images,
                    createdAt           = r.CreatedAt,
                    sellerReply         = r.SellerReply,
                    replyAt             = r.ReplyAt,
                    isVerifiedPurchase  = r.IsVerifiedPurchase
                })
                .ToListAsync();

            return Ok(new {
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit),
                items
            });
        }

        // POST /api/reviews/{reviewId}/reply
        [HttpPost("{reviewId:int}/reply")]
        public async Task<IActionResult> Reply(int reviewId, [FromBody] ReplyReviewDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Reply))
                return BadRequest(new { message = "Nội dung phản hồi không được rỗng" });
            if (dto.Reply.Length > 500)
                return BadRequest(new { message = "Phản hồi tối đa 500 ký tự" });

            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await GetShopProductIdsAsync(shop!.Id);

            var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && productIds.Contains(r.ProductId));
            if (review == null) return NotFound(new { message = "Đánh giá không tồn tại hoặc không thuộc shop của bạn" });
            if (review.SellerReply != null) return BadRequest(new { message = "Bạn đã phản hồi đánh giá này rồi" });

            review.SellerReply = dto.Reply.Trim();
            review.ReplyAt     = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Phản hồi đã được gửi" });
        }

        // GET /api/reviews/shop/stats
        [HttpGet("shop/stats")]
        public async Task<IActionResult> GetStats()
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await GetShopProductIdsAsync(shop!.Id);

            var reviews = await _db.Reviews
                .Where(r => productIds.Contains(r.ProductId))
                .ToListAsync();

            var total = reviews.Count;
            var avg   = total > 0 ? reviews.Average(r => r.Rating) : 0.0;

            return Ok(new {
                totalReviews  = total,
                avgRating     = Math.Round(avg, 1),
                rating5Count  = reviews.Count(r => r.Rating == 5),
                rating4Count  = reviews.Count(r => r.Rating == 4),
                rating3Count  = reviews.Count(r => r.Rating == 3),
                rating2Count  = reviews.Count(r => r.Rating == 2),
                rating1Count  = reviews.Count(r => r.Rating == 1),
                pendingReply  = reviews.Count(r => r.SellerReply == null)
            });
        }
    }

    // ─── Customer: GET with distribution + POST with verified purchase ─────────
    [Route("api/reviews")]
    [ApiController]
    public class CustomerReviewsController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly NotificationService _notificationService;

        public CustomerReviewsController(MySqlDbContext db, NotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
                                    ?? User.FindFirstValue("sub");

        // GET /api/reviews/product/{productId}?page=1&limit=5&rating=&hasImage=
        [HttpGet("product/{productId:int}")]
        public async Task<IActionResult> GetByProduct(
            int productId,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 5,
            [FromQuery] int? rating = null,
            [FromQuery] bool? hasImage = null)
        {
            var query = _db.Reviews
                .Include(r => r.User)
                .Where(r => r.ProductId == productId)
                .AsQueryable();

            if (rating.HasValue)    query = query.Where(r => r.Rating == rating.Value);
            if (hasImage == true)   query = query.Where(r => r.Images != null && r.Images != "");

            var total = await query.CountAsync();
            var allForDist = await _db.Reviews.Where(r => r.ProductId == productId).ToListAsync();
            var avgRating  = allForDist.Count > 0 ? Math.Round(allForDist.Average(r => r.Rating), 1) : 0.0;

            var dist = new Dictionary<int, int> { {5,0},{4,0},{3,0},{2,0},{1,0} };
            foreach (var rv in allForDist)
                dist[Math.Clamp(rv.Rating, 1, 5)]++;

            var items = await query
                .OrderByDescending(r => r.IsVerifiedPurchase)
                .ThenByDescending(r => r.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(r => new {
                    r.Id, r.ProductId,
                    customerName    = r.User != null ? (r.User.Name ?? r.User.UserName) : r.UserId,
                    r.Rating, r.Comment,
                    r.Images,
                    r.IsVerifiedPurchase,
                    r.CreatedAt,
                    sellerReply     = r.SellerReply,
                    replyAt         = r.ReplyAt
                })
                .ToListAsync();

            return Ok(new {
                totalReviews = allForDist.Count,
                avgRating,
                ratingDistribution = dist,
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit),
                reviews = items
            });
        }

        // POST /api/reviews  — Requires login + đã mua và đã nhận hàng
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            if (dto.Rating < 1 || dto.Rating > 5)
                return BadRequest(new { message = "Rating phải từ 1 đến 5" });
            if (dto.ProductId <= 0)
                return BadRequest(new { message = "ProductId không hợp lệ" });

            var userId = GetUserId()!;

            var product = await _db.Products.FindAsync(dto.ProductId);
            if (product == null) return NotFound(new { message = "Sản phẩm không tồn tại" });

            // Kiểm tra đã mua và đã nhận hàng (COMPLETED)
            var hasCompletedOrder = await _db.OrderDetails
                .Include(od => od.Order)
                .AnyAsync(od => od.ProductId == dto.ProductId
                    && od.Order.UserId == userId
                    && od.Order.Status == OrderStatus.Completed);

            if (!hasCompletedOrder)
                return BadRequest(new { message = "Bạn chỉ có thể đánh giá sản phẩm đã mua và đã nhận hàng" });

            // 1 đơn hàng cụ thể chỉ review 1 lần (nếu có orderId)
            if (dto.OrderId.HasValue)
            {
                var orderReviewed = await _db.Reviews
                    .AnyAsync(r => r.ProductId == dto.ProductId && r.UserId == userId);
                if (orderReviewed)
                    return BadRequest(new { message = "Bạn đã đánh giá sản phẩm này rồi" });
            }

            var existing = await _db.Reviews
                .FirstOrDefaultAsync(r => r.ProductId == dto.ProductId && r.UserId == userId);

            if (existing != null)
            {
                existing.Rating    = dto.Rating;
                existing.Comment   = dto.Comment ?? existing.Comment;
                existing.Images    = dto.Images != null ? string.Join(",", dto.Images.Take(3)) : existing.Images;
                existing.CreatedAt = DateTime.Now;
                existing.IsVerifiedPurchase = true;
                await _db.SaveChangesAsync();
                return Ok(existing);
            }

            var review = new Review
            {
                ProductId           = dto.ProductId,
                UserId              = userId,
                Rating              = dto.Rating,
                Comment             = dto.Comment ?? "",
                Images              = dto.Images != null ? string.Join(",", dto.Images.Take(3)) : null,
                CreatedAt           = DateTime.Now,
                IsVerifiedPurchase  = true
            };
            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            // Notify seller
            if (product.ShopId != null)
            {
                var shop = await _db.Shops.FindAsync(product.ShopId);
                if (shop != null)
                {
                    await _notificationService.CreateAsync(
                        shop.SellerId,
                        NotificationType.NewReview,
                        "Đánh giá mới",
                        $"Sản phẩm \"{product.Name}\" nhận đánh giá {review.Rating} sao",
                        "seller-dashboard.html#reviews"
                    );
                }
            }

            return Ok(review);
        }

    }

    public class ReviewDto      { public int Rating { get; set; }  public string? Comment { get; set; }  public List<string>? Images { get; set; } }
    public class ReplyReviewDto { public string Reply { get; set; } = ""; }
    public class UpdateReviewDto { public int Rating { get; set; } public string? Comment { get; set; } }
    public class CreateReviewDto
    {
        public int ProductId { get; set; }
        public int? OrderId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public List<string>? Images { get; set; }
    }
}
