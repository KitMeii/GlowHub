using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CompareController : ControllerBase
    {
        // In-memory compare store per user (resets on server restart — fine for school project)
        private static readonly ConcurrentDictionary<string, HashSet<int>> _store = new();

        private readonly MySqlDbContext _db;

        public CompareController(MySqlDbContext db) => _db = db;

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        /// <summary>POST /api/compare/add — Thêm sản phẩm vào so sánh (tối đa 3)</summary>
        [HttpPost("add")]
        public IActionResult Add([FromBody] AddCompareDto dto)
        {
            var set = _store.GetOrAdd(UserId, _ => new HashSet<int>());
            lock (set)
            {
                if (set.Count >= 3 && !set.Contains(dto.ProductId))
                    return BadRequest(new { message = "Chỉ so sánh tối đa 3 sản phẩm" });
                set.Add(dto.ProductId);
            }
            return Ok(new { count = set.Count, ids = set.ToList() });
        }

        /// <summary>DELETE /api/compare/clear — Xóa tất cả</summary>
        [HttpDelete("clear")]
        public IActionResult Clear()
        {
            _store.TryRemove(UserId, out _);
            return Ok(new { message = "Đã xóa danh sách so sánh" });
        }

        /// <summary>DELETE /api/compare/{productId} — Xóa 1 sản phẩm</summary>
        [HttpDelete("{productId:int}")]
        public IActionResult Remove(int productId)
        {
            if (_store.TryGetValue(UserId, out var set))
                lock (set) { set.Remove(productId); }
            return Ok();
        }

        /// <summary>GET /api/compare — Lấy danh sách sản phẩm đang so sánh</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            if (!_store.TryGetValue(UserId, out var set) || set.Count == 0)
                return Ok(new List<object>());

            List<int> ids;
            lock (set) { ids = set.ToList(); }

            var products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.Shop)
                .Where(p => ids.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            // Calculate avg rating per product
            var productIds = products.Select(p => p.Id).ToList();
            var ratings = await _db.Reviews
                .Where(r => productIds.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new { ProductId = g.Key, Avg = g.Average(r => r.Rating) })
                .ToListAsync();

            return Ok(products.Select(p =>
            {
                var rating = ratings.FirstOrDefault(r => r.ProductId == p.Id);
                return new
                {
                    id = p.Id,
                    name = p.Name,
                    image = p.ImageUrl,
                    price = p.Price,
                    discountPrice = p.DiscountPrice,
                    avgRating = rating != null ? Math.Round(rating.Avg, 1) : 0,
                    soldCount = p.SoldCount,
                    stock = p.Stock,
                    category = p.Category?.Name,
                    specifications = p.Specifications,
                    shopName = p.Shop?.ShopName,
                    shopRating = 0
                };
            }));
        }
    }

    public class AddCompareDto
    {
        public int ProductId { get; set; }
    }
}
