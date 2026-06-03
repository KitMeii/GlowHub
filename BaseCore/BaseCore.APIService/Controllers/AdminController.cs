using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public AdminController(MySqlDbContext db) => _db = db;

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        /// <summary>GET /api/admin/dashboard — Tổng quan hệ thống</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var last7 = today.AddDays(-6);

            // User stats
            var totalUsers   = await _db.Users.CountAsync();
            var totalSellers = await _db.Users.CountAsync(u => u.UserType == 2);
            var totalAdmins  = await _db.Users.CountAsync(u => u.UserType == 1);
            var newUsersToday = await _db.Users.CountAsync(u => u.Created.Date == today);

            // Shop stats
            var totalShops   = await _db.Shops.CountAsync();
            var pendingShops = await _db.Shops.CountAsync(s => s.Status == ShopStatus.Pending);
            var activeShops  = await _db.Shops.CountAsync(s => s.Status == ShopStatus.Active);

            // Product stats
            var totalProducts  = await _db.Products.CountAsync();
            var activeProducts = await _db.Products.CountAsync(p => p.IsActive);

            // Order stats
            var allOrders = await _db.Orders
                .Include(o => o.OrderDetails)
                .ToListAsync();

            var totalOrders    = allOrders.Count;
            var todayOrders    = allOrders.Count(o => o.OrderDate.Date == today);
            var pendingOrders  = allOrders.Count(o => o.Status == OrderStatus.Pending);
            var completedOrders = allOrders.Count(o => o.Status == OrderStatus.Completed);
            var cancelledOrders = allOrders.Count(o => o.Status == OrderStatus.Cancelled);

            // Revenue — chỉ đơn COMPLETED
            var totalRevenue   = allOrders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);
            var todayRevenue   = allOrders.Where(o => o.Status == OrderStatus.Completed && o.OrderDate.Date == today).Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);
            var monthRevenue   = allOrders.Where(o => o.Status == OrderStatus.Completed && o.OrderDate >= monthStart).Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);

            // Revenue chart — last 7 days
            var revenueChart = Enumerable.Range(0, 7).Select(i =>
            {
                var day = today.AddDays(-6 + i);
                var dayRev = allOrders
                    .Where(o => o.Status == OrderStatus.Completed && o.OrderDate.Date == day)
                    .Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);
                var dayOrders = allOrders.Count(o => o.OrderDate.Date == day);
                return new { date = day.ToString("yyyy-MM-dd"), revenue = dayRev, orders = dayOrders };
            });

            // Recent 10 orders
            var recentOrders = allOrders
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .Select(o => new {
                    orderId   = o.Id,
                    orderCode = o.OrderCode ?? ("ORD-" + o.Id.ToString("D6")),
                    status    = o.Status,
                    amount    = o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount,
                    createdAt = DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc)
                });

            // Top 5 shops by revenue — SubOrders as source of truth
            var topShopRevData = await _db.SubOrders
                .Where(s => s.ShopId != null && s.Order.Status == OrderStatus.Completed)
                .GroupBy(s => s.ShopId!)
                .Select(g => new { shopId = g.Key, revenue = g.Sum(s => s.ProductRevenue) })
                .OrderByDescending(x => x.revenue)
                .Take(5)
                .ToListAsync();
            var topShopIds  = topShopRevData.Select(x => x.shopId).ToList();
            var topShopDict = await _db.Shops
                .Where(s => topShopIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.ShopName);
            var topShops = topShopRevData.Select(x => new {
                shopId   = x.shopId,
                shopName = topShopDict.TryGetValue(x.shopId, out var sn) ? sn : x.shopId,
                revenue  = x.revenue
            });

            return Ok(new {
                users = new { total = totalUsers, sellers = totalSellers, admins = totalAdmins, newToday = newUsersToday },
                shops = new { total = totalShops, pending = pendingShops, active = activeShops },
                products = new { total = totalProducts, active = activeProducts },
                orders = new {
                    total = totalOrders,
                    today = todayOrders,
                    pending = pendingOrders,
                    completed = completedOrders,
                    cancelled = cancelledOrders
                },
                revenue = new { total = totalRevenue, today = todayRevenue, month = monthRevenue },
                revenueChart,
                recentOrders,
                topShops
            });
        }

        /// <summary>GET /api/admin/stats/revenue?from=&amp;to= — Biểu đồ doanh thu theo khoảng ngày</summary>
        [HttpGet("stats/revenue")]
        public async Task<IActionResult> GetRevenueStats([FromQuery] string? from, [FromQuery] string? to)
        {
            var fromDate = DateTime.TryParse(from, out var f)
                ? f.ToUniversalTime().Date
                : DateTime.UtcNow.AddDays(-29).Date;
            var toDate = DateTime.TryParse(to, out var t)
                ? t.ToUniversalTime().Date
                : DateTime.UtcNow.Date;

            var orders = await _db.Orders
                .Where(o => o.Status == OrderStatus.Completed
                         && o.OrderDate.Date >= fromDate
                         && o.OrderDate.Date <= toDate)
                .ToListAsync();

            var days = (toDate - fromDate).Days + 1;
            var chart = Enumerable.Range(0, days).Select(i =>
            {
                var day = fromDate.AddDays(i);
                var dayOrders = orders.Where(o => o.OrderDate.Date == day).ToList();
                var revenue = dayOrders.Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);
                return new { date = day.ToString("yyyy-MM-dd"), orders = dayOrders.Count, revenue };
            });

            var totalRevenue = orders.Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);
            var totalOrders  = orders.Count;

            // Group by payment method
            var byPayment = orders
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new { method = g.Key, count = g.Count(), revenue = g.Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount) });

            return Ok(new {
                from = fromDate.ToString("yyyy-MM-dd"),
                to   = toDate.ToString("yyyy-MM-dd"),
                totalRevenue,
                totalOrders,
                chart,
                byPayment
            });
        }

        /// <summary>GET /api/admin/stats/summary — Số liệu nhanh cho header cards</summary>
        [HttpGet("stats/summary")]
        public async Task<IActionResult> GetSummary()
        {
            var today = DateTime.UtcNow.Date;

            var totalUsers    = await _db.Users.CountAsync();
            var totalProducts = await _db.Products.CountAsync(p => p.IsActive);
            var totalOrders   = await _db.Orders.CountAsync();
            var pendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending);

            var totalRevenue = await _db.Orders
                .Where(o => o.Status == OrderStatus.Completed)
                .SumAsync(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount);

            var pendingShops = await _db.Shops.CountAsync(s => s.Status == ShopStatus.Pending);
            var activeFlashSale = await _db.FlashSales.CountAsync(fs => fs.IsActive && fs.StartTime <= DateTime.UtcNow && fs.EndTime > DateTime.UtcNow);

            return Ok(new {
                totalUsers,
                totalProducts,
                totalOrders,
                pendingOrders,
                totalRevenue,
                pendingShops,
                activeFlashSale
            });
        }
    }
}
