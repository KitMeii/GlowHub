using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    // Endpoint tổng hợp duy nhất cho tab Thống Kê — gom mọi metric vào 1 round-trip
    // và đẩy hết aggregation xuống DB (GroupBy/Sum/Count) thay vì kéo list về client.
    [Route("api/admin/stats")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminStatsController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public AdminStatsController(MySqlDbContext db)
        {
            _db = db;
        }

        /// <summary>GET /api/admin/stats/overview?from=&to=&topShopLimit= — Tất cả số liệu Thống Kê trong 1 lần gọi</summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(
            [FromQuery] string? from         = null,
            [FromQuery] string? to           = null,
            [FromQuery] int     topShopLimit = 10)
        {
            // ── Date range (inclusive on both ends, default 30 ngày) ──
            var fromDate = DateTime.TryParse(from, out var f)
                ? DateTime.SpecifyKind(f.Date, DateTimeKind.Utc)
                : DateTime.UtcNow.Date.AddDays(-29);
            var toDateExclusive = DateTime.TryParse(to, out var t)
                ? DateTime.SpecifyKind(t.Date.AddDays(1), DateTimeKind.Utc)
                : DateTime.UtcNow.Date.AddDays(1);

            var ordersInRange = _db.Orders
                .Where(o => o.OrderDate >= fromDate && o.OrderDate < toDateExclusive);

            // ── Doanh thu theo ngày (loại CANCELLED) ──
            var revenueByDayRaw = await ordersInRange
                .Where(o => o.Status != OrderStatus.Cancelled)
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new {
                    date        = g.Key,
                    revenue     = g.Sum(o => o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount + o.ShippingFee),
                    orderCount  = g.Count()
                })
                .ToListAsync();

            // Fill các ngày trống để client không phải zero-fill nữa
            var byDayDict = revenueByDayRaw.ToDictionary(x => x.date, x => x);
            var revenueByDay = new List<object>();
            for (var d = fromDate; d < toDateExclusive; d = d.AddDays(1))
            {
                if (byDayDict.TryGetValue(d, out var hit))
                    revenueByDay.Add(new { date = d, hit.revenue, hit.orderCount });
                else
                    revenueByDay.Add(new { date = d, revenue = 0m, orderCount = 0 });
            }

            // ── Đếm theo trạng thái ──
            var statusCountsRaw = await ordersInRange
                .GroupBy(o => o.Status)
                .Select(g => new { status = g.Key, count = g.Count() })
                .ToListAsync();
            var statusCounts = new Dictionary<string, int> {
                { OrderStatus.Pending,   0 },
                { OrderStatus.Confirmed, 0 },
                { OrderStatus.Shipping,  0 },
                { OrderStatus.Delivered, 0 },
                { OrderStatus.Completed, 0 },
                { OrderStatus.Cancelled, 0 }
            };
            foreach (var s in statusCountsRaw)
                statusCounts[s.status] = s.count;

            // ── Tổng (loại CANCELLED khi tính doanh thu/khách) ──
            var totalRevenue = revenueByDayRaw.Sum(x => x.revenue);
            var totalOrders  = await ordersInRange.CountAsync();
            var uniqueCustomers = await ordersInRange
                .Where(o => o.Status != OrderStatus.Cancelled)
                .Select(o => o.UserId)
                .Distinct()
                .CountAsync();

            // ── Top shops theo SubOrder (per-shop financials, theo Admin Guide) ──
            var topShopsAgg = await _db.SubOrders
                .Where(s => s.ShopId != null
                         && s.Order.OrderDate >= fromDate
                         && s.Order.OrderDate < toDateExclusive
                         && s.Order.Status != OrderStatus.Cancelled)
                .GroupBy(s => s.ShopId!)
                .Select(g => new {
                    shopId           = g.Key,
                    revenue          = g.Sum(s => s.ProductRevenue),
                    commissionAmount = g.Sum(s => s.CommissionAmount),
                    netRevenue       = g.Sum(s => s.SellerPayoutAmount),
                    orderCount       = g.Select(s => s.OrderId).Distinct().Count()
                })
                .OrderByDescending(x => x.revenue)
                .Take(topShopLimit)
                .ToListAsync();

            var topShopIds = topShopsAgg.Select(x => x.shopId).ToList();
            var shopNames = await _db.Shops
                .Where(s => topShopIds.Contains(s.Id))
                .Select(s => new { s.Id, s.ShopName, s.Logo })
                .ToListAsync();
            var nameDict = shopNames.ToDictionary(x => x.Id);

            var topShops = topShopsAgg.Select(x => {
                nameDict.TryGetValue(x.shopId, out var n);
                return new {
                    shopId   = x.shopId,
                    shopName = n?.ShopName ?? x.shopId,
                    logo     = n?.Logo,
                    x.revenue,
                    x.commissionAmount,
                    x.netRevenue,
                    x.orderCount
                };
            });

            return Ok(new {
                range = new { from = fromDate, to = toDateExclusive.AddTicks(-1) },
                revenueByDay,
                orderStatusCounts = statusCounts,
                topShops,
                totals = new {
                    revenue   = totalRevenue,
                    orders    = totalOrders,
                    customers = uniqueCustomers
                }
            });
        }
    }
}
