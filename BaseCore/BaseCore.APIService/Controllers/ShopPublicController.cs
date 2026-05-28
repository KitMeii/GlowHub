using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;
using BaseCore.Repository;

namespace BaseCore.APIService.Controllers
{
    [Route("api/shops/{shopId}")]
    [ApiController]
    public class ShopPublicController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public ShopPublicController(MySqlDbContext db)
        {
            _db = db;
        }

        private async Task<Shop?> GetActiveShop(string shopId)
        {
            var shop = await _db.Shops.FindAsync(shopId);
            return shop?.Status == ShopStatus.Active ? shop : null;
        }

        // GET /api/shops/{shopId}/profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile(string shopId)
        {
            var shop = await GetActiveShop(shopId);
            if (shop == null) return NotFound(new { message = "Shop không tồn tại hoặc chưa hoạt động" });

            var productIds = await _db.Products
                .Where(p => p.ShopId == shopId && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();

            var totalSold = await _db.OrderDetails
                .Where(od => productIds.Contains(od.ProductId))
                .SumAsync(od => (int?)od.Quantity) ?? 0;

            var reviews = await _db.Reviews
                .Where(r => productIds.Contains(r.ProductId))
                .ToListAsync();

            var avgRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 0.0;

            return Ok(new {
                shopId       = shop.Id,
                shopName     = shop.ShopName,
                logo         = shop.Logo,
                description  = shop.Description,
                address      = shop.Address,
                phone        = shop.Phone,
                status       = shop.Status,
                totalProducts = productIds.Count,
                totalSold,
                avgRating,
                totalReviews  = reviews.Count,
                // SpecifyKind=Utc ensures System.Text.Json appends 'Z' so JS parses as UTC
                joinedDate    = DateTime.SpecifyKind(shop.CreatedAt, DateTimeKind.Utc),
                isActive      = shop.Status == ShopStatus.Active
            });
        }

        // GET /api/shops/{shopId}/products?page=1&limit=12&categoryId=&minPrice=&maxPrice=&sort=
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(
            string shopId,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 12,
            [FromQuery] int? categoryId = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string sort = "newest")
        {
            var shop = await GetActiveShop(shopId);
            if (shop == null) return NotFound(new { message = "Shop không tồn tại hoặc chưa hoạt động" });

            var query = _db.Products
                .Include(p => p.Category)
                .Where(p => p.ShopId == shopId && p.IsActive)
                .AsQueryable();

            if (categoryId.HasValue)  query = query.Where(p => p.CategoryId == categoryId.Value);
            if (minPrice.HasValue)    query = query.Where(p => (p.DiscountPrice ?? p.Price) >= minPrice.Value);
            if (maxPrice.HasValue)    query = query.Where(p => (p.DiscountPrice ?? p.Price) <= maxPrice.Value);

            query = sort switch {
                "price-asc"   => query.OrderBy(p => p.DiscountPrice ?? p.Price),
                "price-desc"  => query.OrderByDescending(p => p.DiscountPrice ?? p.Price),
                "best-seller" => query.OrderByDescending(p => p.SoldCount),
                _             => query.OrderByDescending(p => p.CreatedAt)
            };

            var total = await query.CountAsync();
            var products = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var ids = products.Select(p => p.Id).ToList();
            var avgRatings = await _db.Reviews
                .Where(r => ids.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new { pid = g.Key, avg = g.Average(r => (double)r.Rating), cnt = g.Count() })
                .ToDictionaryAsync(x => x.pid, x => new { x.avg, x.cnt });

            var items = products.Select(p => {
                avgRatings.TryGetValue(p.Id, out var rv);
                var finalPrice = p.DiscountPrice ?? p.Price;
                var disc = p.DiscountPrice.HasValue && p.Price > 0
                    ? (int)Math.Round((1 - (double)p.DiscountPrice.Value / (double)p.Price) * 100)
                    : 0;
                return new {
                    p.Id, p.Name, p.Price,
                    discountPrice = p.DiscountPrice,
                    image = p.ImageUrl,
                    avgRating     = rv != null ? Math.Round(rv.avg, 1) : 0.0,
                    reviewCount   = rv?.cnt ?? 0,
                    soldCount     = p.SoldCount,
                    isNew         = p.IsNew,
                    discountPct   = disc,
                    categoryName  = p.Category?.Name ?? ""
                };
            });

            return Ok(new {
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit),
                items
            });
        }

        // GET /api/shops/{shopId}/reviews?page=1&limit=10&rating=
        [HttpGet("reviews")]
        public async Task<IActionResult> GetReviews(
            string shopId,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            [FromQuery] int? rating = null)
        {
            var shop = await GetActiveShop(shopId);
            if (shop == null) return NotFound(new { message = "Shop không tồn tại hoặc chưa hoạt động" });

            var productIds = await _db.Products
                .Where(p => p.ShopId == shopId && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();

            var query = _db.Reviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .Where(r => productIds.Contains(r.ProductId))
                .AsQueryable();

            if (rating.HasValue) query = query.Where(r => r.Rating == rating.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(r => new {
                    r.Id,
                    customerName  = r.User != null ? (r.User.Name ?? r.User.UserName) : r.UserId,
                    r.Rating,
                    r.Comment,
                    r.Images,
                    r.IsVerifiedPurchase,
                    r.CreatedAt,
                    productName   = r.Product != null ? r.Product.Name : "",
                    productImage  = r.Product != null ? r.Product.ImageUrl : "",
                    sellerReply   = r.SellerReply,
                    replyAt       = r.ReplyAt
                })
                .ToListAsync();

            return Ok(new { total, page, totalPages = (int)Math.Ceiling((double)total / limit), items });
        }

        // GET /api/shops/{shopId}/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(string shopId)
        {
            var shop = await GetActiveShop(shopId);
            if (shop == null) return NotFound(new { message = "Shop không tồn tại hoặc chưa hoạt động" });

            var productIds = await _db.Products
                .Where(p => p.ShopId == shopId && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();

            var totalSold = await _db.OrderDetails
                .Where(od => productIds.Contains(od.ProductId))
                .SumAsync(od => (int?)od.Quantity) ?? 0;

            var reviews = await _db.Reviews
                .Where(r => productIds.Contains(r.ProductId))
                .ToListAsync();

            var totalQnA = await _db.QnAs
                .Where(q => productIds.Contains(q.ProductId) && q.IsActive)
                .CountAsync();

            var answeredQnA = await _db.QnAs
                .Where(q => productIds.Contains(q.ProductId) && q.IsActive && q.Answer != null)
                .CountAsync();

            var responseRate = totalQnA > 0 ? Math.Round((double)answeredQnA / totalQnA * 100, 1) : 0.0;
            var avgRating    = reviews.Count > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 0.0;
            var joinedDays   = (int)(DateTime.UtcNow - shop.CreatedAt).TotalDays;

            return Ok(new {
                totalProducts = productIds.Count,
                totalSold,
                avgRating,
                totalReviews  = reviews.Count,
                responseRate,
                joinedDays
            });
        }
    }
}
