using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Common;
using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShopsController : ControllerBase
    {
        private readonly IShopService _shopService;
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;

        public ShopsController(IShopService shopService, MySqlDbContext db, AuditLogService audit)
        {
            _shopService = shopService;
            _db          = db;
            _audit       = audit;
        }

        private string? GetSellerId()  => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserId()    => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName()  => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

        // ─────────────────────────────────────────────────────────
        // PUBLIC
        // ─────────────────────────────────────────────────────────

        // GET /api/shops/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var shop = await _shopService.GetByIdAsync(id);
            if (shop == null) return NotFound(new { message = "Shop not found" });
            return Ok(shop);
        }

        // ─────────────────────────────────────────────────────────
        // SELLER — my shop
        // ─────────────────────────────────────────────────────────

        // GET /api/shops/my
        [HttpGet("my")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetMy()
        {
            var sellerId = GetSellerId();
            if (string.IsNullOrEmpty(sellerId)) return Unauthorized();
            var shop = await _shopService.GetBySellerIdAsync(sellerId);
            if (shop == null) return NotFound(new { message = "Bạn chưa có shop" });
            return Ok(shop);
        }

        // GET /api/shops/my/dashboard
        [HttpGet("my/dashboard")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetDashboard()
        {
            var sellerId = GetSellerId();
            var shop = await _shopService.GetBySellerIdAsync(sellerId!);
            if (shop == null) return NotFound(new { message = "Bạn chưa có shop" });
            if (shop.Status != ShopStatus.Active)
                return BadRequest(new { message = "Shop chưa được duyệt hoặc đã bị khóa" });

            var shopId = shop.Id;
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var weekStart = today.AddDays(-6);

            // SubOrder là source of truth cho per-shop financials (theo Admin Guide §3.6).
            // Tránh load toàn bộ Orders + Include nested vào memory.
            var subQ = _db.SubOrders.Where(s => s.ShopId == shopId);

            var todayRevenue = await subQ
                .Where(s => s.Status == OrderStatus.Completed
                         && s.Order.OrderDate >= today && s.Order.OrderDate < tomorrow)
                .SumAsync(s => (decimal?)s.ProductRevenue) ?? 0m;

            var monthRevenue = await subQ
                .Where(s => s.Status == OrderStatus.Completed && s.Order.OrderDate >= monthStart)
                .SumAsync(s => (decimal?)s.ProductRevenue) ?? 0m;

            var totalOrders   = await subQ.CountAsync();
            var pendingOrders = await subQ.CountAsync(s => s.Status == OrderStatus.Pending);

            var totalProducts = await _db.Products.CountAsync(p => p.ShopId == shopId);
            var lowStockCount = await _db.Products.CountAsync(p => p.ShopId == shopId && p.Stock < 10 && p.IsActive);

            // Recent 5 sub-orders — chỉ load những field cần
            var recentOrders = await subQ
                .OrderByDescending(s => s.Order.OrderDate)
                .Take(5)
                .Select(s => new
                {
                    orderId     = s.OrderId,
                    customer    = s.Order.User != null ? (s.Order.User.Name ?? s.Order.UserId) : s.Order.UserId,
                    totalAmount = s.FinalAmount,
                    status      = s.Status,
                    createdAt   = s.Order.OrderDate
                })
                .ToListAsync();

            // Top 5 products theo sold count (chỉ trong sub-orders của shop này)
            var topProducts = await _db.SubOrderItems
                .Where(soi => soi.SubOrder.ShopId == shopId)
                .GroupBy(soi => soi.ProductId)
                .Select(g => new { productId = g.Key, soldCount = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.soldCount)
                .Take(5)
                .Join(_db.Products, x => x.productId, p => p.Id,
                    (x, p) => new { p.Id, p.Name, p.ImageUrl, p.Price, x.soldCount })
                .ToListAsync();

            // Revenue chart 7 ngày — GroupBy trong DB rồi map sang dải ngày liên tục
            var chartRaw = await subQ
                .Where(s => s.Status == OrderStatus.Completed && s.Order.OrderDate >= weekStart)
                .GroupBy(s => s.Order.OrderDate.Date)
                .Select(g => new { date = g.Key, revenue = g.Sum(s => s.ProductRevenue) })
                .ToListAsync();
            var chartMap = chartRaw.ToDictionary(x => x.date, x => x.revenue);
            var revenueChart = Enumerable.Range(0, 7).Select(i =>
            {
                var day = today.AddDays(-6 + i);
                return new
                {
                    date    = day.ToString("yyyy-MM-dd"),
                    revenue = chartMap.TryGetValue(day, out var r) ? r : 0m
                };
            });

            return Ok(new
            {
                todayRevenue,
                monthRevenue,
                totalOrders,
                pendingOrders,
                totalProducts,
                lowStockCount,
                recentOrders,
                topProducts,
                revenueChart
            });
        }

        // GET /api/shops/my/stats?from=&to=
        [HttpGet("my/stats")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetStats([FromQuery] string? from, [FromQuery] string? to)
        {
            var sellerId = GetSellerId();
            var shop = await _shopService.GetBySellerIdAsync(sellerId!);
            if (shop == null) return NotFound(new { message = "Bạn chưa có shop" });
            if (shop.Status != ShopStatus.Active)
                return BadRequest(new { message = "Shop chưa được duyệt hoặc đã bị khóa" });

            var fromDate = DateTime.TryParse(from, out var f)
                ? f.ToUniversalTime().Date
                : DateTime.UtcNow.AddDays(-29).Date;
            var toDate = DateTime.TryParse(to, out var t)
                ? t.ToUniversalTime().Date
                : DateTime.UtcNow.Date;

            var shopId = shop.Id;
            var productIds = await _db.Products
                .Where(p => p.ShopId == shopId)
                .Select(p => p.Id)
                .ToListAsync();

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.Status == OrderStatus.Completed
                         && o.OrderDate.Date >= fromDate
                         && o.OrderDate.Date <= toDate
                         && o.OrderDetails.Any(od => productIds.Contains(od.ProductId)))
                .ToListAsync();

            var days = (toDate - fromDate).Days + 1;
            var chart = Enumerable.Range(0, days).Select(i =>
            {
                var day = fromDate.AddDays(i);
                var dayOrders = orders.Where(o => o.OrderDate.Date == day).ToList();
                var revenue = dayOrders.Sum(o => o.OrderDetails
                    .Where(od => productIds.Contains(od.ProductId))
                    .Sum(od => od.UnitPrice * od.Quantity));
                var commission = revenue * (shop.CommissionRate / 100m);
                return new
                {
                    date       = day.ToString("yyyy-MM-dd"),
                    orderCount = dayOrders.Count,
                    revenue,
                    commission,
                    net        = revenue - commission
                };
            });

            var totalRevenue  = chart.Sum(x => x.revenue);
            var totalCommission = chart.Sum(x => x.commission);

            return Ok(new
            {
                from     = fromDate.ToString("yyyy-MM-dd"),
                to       = toDate.ToString("yyyy-MM-dd"),
                commissionRate = shop.CommissionRate,
                totalRevenue,
                totalCommission,
                totalNet = totalRevenue - totalCommission,
                totalOrders = orders.Count,
                chart
            });
        }

        // POST /api/shops/register
        [HttpPost("register")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> Register([FromBody] RegisterShopRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ShopName))
                return BadRequest(new { message = "ShopName là bắt buộc" });

            var sellerId = GetSellerId();
            if (string.IsNullOrEmpty(sellerId)) return Unauthorized();

            try
            {
                var shop = await _shopService.RegisterAsync(
                    sellerId, req.ShopName, req.Description, req.Logo, req.Address, req.Phone);
                // Set Province + Region if provided
                if (!string.IsNullOrEmpty(req.Province))
                {
                    shop.Province = req.Province;
                    shop.Region   = !string.IsNullOrEmpty(req.Region)
                        ? req.Region.ToUpper()
                        : BaseCore.Services.ShippingRegion.Normalize(req.Province);
                    await _shopService.UpdateAsync(shop);
                }
                return Ok(new { message = "Đăng ký shop thành công. Chờ admin duyệt.", shopId = shop.Id });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        // PUT /api/shops/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateShopRequest req)
        {
            var sellerId = GetSellerId();
            var shop = await _shopService.GetByIdAsync(id);
            if (shop == null) return NotFound(new { message = "Shop not found" });
            if (shop.SellerId != sellerId) return Forbid();

            if (!string.IsNullOrWhiteSpace(req.ShopName)) shop.ShopName = req.ShopName;
            if (req.Description != null) shop.Description = req.Description;
            if (req.Logo != null) shop.Logo = req.Logo;
            if (req.Address != null) shop.Address = req.Address;
            if (req.Phone != null) shop.Phone = req.Phone;
            if (req.Province != null) {
                shop.Province = req.Province;
                // Derive Region from Province if not explicitly provided
                shop.Region = !string.IsNullOrEmpty(req.Region)
                    ? req.Region.ToUpper()
                    : BaseCore.Services.ShippingRegion.Normalize(req.Province);
            }

            await _shopService.UpdateAsync(shop);
            return Ok(new { message = "Shop đã được cập nhật" });
        }

        // ─────────────────────────────────────────────────────────
        // FOLLOW
        // ─────────────────────────────────────────────────────────

        // POST /api/shops/{id}/follow
        [HttpPost("{id}/follow")]
        [Authorize]
        public async Task<IActionResult> Follow(string id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var shop = await _db.Shops.FindAsync(id);
            if (shop == null) return NotFound(new { message = "Không tìm thấy shop" });

            var exists = await _db.ShopFollows
                .AnyAsync(f => f.UserId == userId && f.ShopId == id);
            if (!exists)
            {
                _db.ShopFollows.Add(new ShopFollow { UserId = userId, ShopId = id });
                await _db.SaveChangesAsync();
            }
            return Ok(new { message = "Đã theo dõi shop", shopId = id });
        }

        // DELETE /api/shops/{id}/follow
        [HttpDelete("{id}/follow")]
        [Authorize]
        public async Task<IActionResult> Unfollow(string id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var follow = await _db.ShopFollows
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ShopId == id);
            if (follow != null)
            {
                _db.ShopFollows.Remove(follow);
                await _db.SaveChangesAsync();
            }
            return Ok(new { message = "Đã bỏ theo dõi shop" });
        }

        // GET /api/shops/followed
        [HttpGet("followed")]
        [Authorize]
        public async Task<IActionResult> GetFollowed()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var follows = await _db.ShopFollows
                .Where(f => f.UserId == userId)
                .Include(f => f.Shop)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    shopId   = f.ShopId,
                    shopName = f.Shop.ShopName,
                    logo     = f.Shop.Logo,
                    followedAt = f.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items = follows, total = follows.Count });
        }

        // ─────────────────────────────────────────────────────────
        // ADMIN
        // ─────────────────────────────────────────────────────────

        // GET /api/shops/admin/all
        [HttpGet("admin/all")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var (shops, total) = await _shopService.GetAllPagedAsync(page, pageSize);
            return Ok(new { shops, total, page, pageSize });
        }

        // PUT /api/shops/admin/{id}/approve
        [HttpPut("admin/{id}/approve")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Approve(string id)
        {
            try
            {
                await _shopService.ApproveAsync(id);
                await _audit.Log(GetUserId(), GetUserName(), "SHOP_APPROVE", "Shop", id,
                    new { status = ShopStatus.Pending }, new { status = ShopStatus.Active });
                return Ok(new { message = "Shop đã được duyệt" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        // PUT /api/shops/admin/{id}/ban
        [HttpPut("admin/{id}/ban")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Ban(string id)
        {
            try
            {
                await _shopService.BanAsync(id);
                await _audit.Log(GetUserId(), GetUserName(), "SHOP_BAN", "Shop", id,
                    new { status = ShopStatus.Active }, new { status = ShopStatus.Banned });
                return Ok(new { message = "Shop đã bị khóa" });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        // GET /api/shops/admin/stats — Tất cả shops với doanh thu / đơn hàng / sản phẩm
        [HttpGet("admin/stats")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> GetAdminShopStats(
            [FromQuery] int status = -1,
            [FromQuery] int page   = 1,
            [FromQuery] int limit  = 20)
        {
            var query = _db.Shops.AsQueryable();
            if (status >= 0) query = query.Where(s => s.Status == status);

            var total = await query.CountAsync();
            var shops = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var shopIds = shops.Select(s => s.Id).ToList();

            // SubOrders as source of truth for per-shop revenue
            var subRevMap = await _db.SubOrders
                .Where(s => shopIds.Contains(s.ShopId!) && s.Order.Status == OrderStatus.Completed)
                .GroupBy(s => s.ShopId!)
                .Select(g => new { shopId = g.Key, revenue = g.Sum(s => s.ProductRevenue) })
                .ToDictionaryAsync(x => x.shopId, x => x.revenue);

            var subOrderCountMap = await _db.SubOrders
                .Where(s => shopIds.Contains(s.ShopId!))
                .GroupBy(s => s.ShopId!)
                .Select(g => new { shopId = g.Key, count = g.Select(s => s.OrderId).Distinct().Count() })
                .ToDictionaryAsync(x => x.shopId, x => x.count);

            var result = new List<object>();
            foreach (var shop in shops)
            {
                var productCount = await _db.Products.CountAsync(p => p.ShopId == shop.Id);
                var revenue    = subRevMap.TryGetValue(shop.Id, out var rev) ? rev : 0m;
                var orderCount = subOrderCountMap.TryGetValue(shop.Id, out var cnt) ? cnt : 0;

                result.Add(new {
                    shop.Id,
                    shop.ShopName,
                    shop.Logo,
                    shop.Status,
                    shop.CommissionRate,
                    shop.CreatedAt,
                    productCount,
                    orderCount,
                    revenue
                });
            }

            return Ok(new { items = result, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        // PUT /api/shops/admin/{id}/commission
        [HttpPut("admin/{id}/commission")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> UpdateCommission(string id, [FromBody] UpdateCommissionDto dto)
        {
            if (dto.CommissionRate < 0 || dto.CommissionRate > 100)
                return BadRequest(new { message = "Commission rate phải từ 0 đến 100" });

            var shop = await _db.Shops.FindAsync(id);
            if (shop == null) return NotFound(new { message = "Không tìm thấy shop" });

            var oldRate = shop.CommissionRate;
            shop.CommissionRate = dto.CommissionRate;
            shop.UpdatedAt      = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _audit.Log(GetUserId(), GetUserName(), "SHOP_COMMISSION", "Shop", id,
                new { commissionRate = oldRate }, new { commissionRate = dto.CommissionRate });
            return Ok(new { message = "Đã cập nhật hoa hồng", commissionRate = dto.CommissionRate });
        }

        // GET /api/shops/admin/{id}/stats
        [HttpGet("admin/{id}/stats")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> GetShopStats(string id, [FromQuery] string? from, [FromQuery] string? to)
        {
            var shop = await _db.Shops.FindAsync(id);
            if (shop == null) return NotFound(new { message = "Không tìm thấy shop" });

            var fromDate = DateTime.TryParse(from, out var f) ? f.ToUniversalTime().Date : DateTime.UtcNow.AddDays(-29).Date;
            var toDate   = DateTime.TryParse(to, out var t) ? t.ToUniversalTime().Date.AddDays(1) : DateTime.UtcNow.Date.AddDays(1);

            // ✅ SubOrder là nguồn sự thật cho doanh thu của shop — aggregate trong DB
            var subsQuery = _db.SubOrders
                .Where(s => s.ShopId == id
                         && s.Order.OrderDate >= fromDate
                         && s.Order.OrderDate < toDate);

            var completedSubs = subsQuery.Where(s => s.Order.Status == OrderStatus.Completed);

            var revenue    = await completedSubs.SumAsync(s => (decimal?)s.ProductRevenue) ?? 0m;
            var commission = await completedSubs.SumAsync(s => (decimal?)s.CommissionAmount) ?? 0m;
            var totalOrders = await subsQuery.Select(s => s.OrderId).Distinct().CountAsync();

            return Ok(new {
                shopId   = id,
                shopName = shop.ShopName,
                from     = fromDate.ToString("yyyy-MM-dd"),
                to       = toDate.AddDays(-1).ToString("yyyy-MM-dd"),
                commissionRate = shop.CommissionRate,
                totalOrders,
                revenue,
                commission,
                net = revenue - commission
            });
        }
    }

    public class RegisterShopRequest
    {
        public string ShopName { get; set; } = "";
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Region   { get; set; }
    }

    public class UpdateShopRequest
    {
        public string? ShopName { get; set; }
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Region   { get; set; }
    }

    public class UpdateCommissionDto
    {
        public decimal CommissionRate { get; set; }
    }
}
