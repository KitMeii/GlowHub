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

        public ShopsController(IShopService shopService, MySqlDbContext db)
        {
            _shopService = shopService;
            _db = db;
        }

        private string? GetSellerId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

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
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var weekAgo = today.AddDays(-6);

            // Product IDs owned by this shop
            var productIds = await _db.Products
                .Where(p => p.ShopId == shopId)
                .Select(p => p.Id)
                .ToListAsync();

            // All orders that contain at least one product from this shop
            var shopOrders = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.User)
                .Where(o => o.OrderDetails.Any(od => productIds.Contains(od.ProductId)))
                .ToListAsync();

            // Revenue helpers — only from COMPLETED orders, only for shop's products
            decimal CalcRevenue(IEnumerable<Order> orders)
                => orders.Where(o => o.Status == OrderStatus.Completed)
                         .Sum(o => o.OrderDetails
                             .Where(od => productIds.Contains(od.ProductId))
                             .Sum(od => od.UnitPrice * od.Quantity));

            var todayOrders = shopOrders.Where(o => o.OrderDate.Date == today);
            var monthOrders = shopOrders.Where(o => o.OrderDate >= monthStart);

            var todayRevenue  = CalcRevenue(todayOrders);
            var monthRevenue  = CalcRevenue(monthOrders);
            var totalOrders   = shopOrders.Count;
            var pendingOrders = shopOrders.Count(o => o.Status == OrderStatus.Pending);

            var totalProducts  = productIds.Count;
            var lowStockCount  = await _db.Products.CountAsync(p => p.ShopId == shopId && p.Stock < 10 && p.IsActive);

            // Recent 5 orders
            var recentOrders = shopOrders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .Select(o => new
                {
                    orderId      = o.Id,
                    customer     = o.User?.Name ?? o.UserId,
                    totalAmount  = o.TotalAmount,
                    status       = o.Status,
                    createdAt    = o.OrderDate
                });

            // Top 5 products by sold count
            var topProducts = await _db.OrderDetails
                .Where(od => productIds.Contains(od.ProductId))
                .GroupBy(od => od.ProductId)
                .Select(g => new { productId = g.Key, soldCount = g.Sum(od => od.Quantity) })
                .OrderByDescending(x => x.soldCount)
                .Take(5)
                .Join(_db.Products, x => x.productId, p => p.Id,
                    (x, p) => new { p.Id, p.Name, p.ImageUrl, p.Price, x.soldCount })
                .ToListAsync();

            // Revenue chart — last 7 days
            var revenueChart = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var day = today.AddDays(-6 + i);
                    var rev = shopOrders
                        .Where(o => o.Status == OrderStatus.Completed && o.OrderDate.Date == day)
                        .Sum(o => o.OrderDetails
                            .Where(od => productIds.Contains(od.ProductId))
                            .Sum(od => od.UnitPrice * od.Quantity));
                    return new { date = day.ToString("yyyy-MM-dd"), revenue = rev };
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

            await _shopService.UpdateAsync(shop);
            return Ok(new { message = "Shop đã được cập nhật" });
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
            try { await _shopService.ApproveAsync(id); return Ok(new { message = "Shop đã được duyệt" }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }

        // PUT /api/shops/admin/{id}/ban
        [HttpPut("admin/{id}/ban")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Ban(string id)
        {
            try { await _shopService.BanAsync(id); return Ok(new { message = "Shop đã bị khóa" }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        }
    }

    public class RegisterShopRequest
    {
        public string ShopName { get; set; } = "";
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
    }

    public class UpdateShopRequest
    {
        public string? ShopName { get; set; }
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
    }
}
