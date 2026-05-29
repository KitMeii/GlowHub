using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Common;
using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Repository.EFCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/reports/seller")]
    [ApiController]
    [Authorize(Roles = RoleConstant.Seller)]
    public class ReportController : ControllerBase
    {
        private readonly IShopRepositoryEF _shopRepository;
        private readonly MySqlDbContext _db;

        public ReportController(IShopRepositoryEF shopRepository, MySqlDbContext db)
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

        // GET /api/reports/seller/revenue?from=&to=
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenue(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to   = null)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var dateFrom = (from ?? DateTime.UtcNow.AddDays(-30)).Date;
            var dateTo   = (to   ?? DateTime.UtcNow).Date.AddDays(1);

            var orders = await _db.Orders
                .Where(o => o.ShopId == shop!.Id
                    && o.OrderDate >= dateFrom && o.OrderDate < dateTo
                    && o.Status != OrderStatus.Cancelled)
                .ToListAsync();

            var grouped = orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new {
                    date        = g.Key.ToString("yyyy-MM-dd"),
                    totalOrders = g.Count(),
                    revenue     = g.Sum(o => o.ProductRevenue),
                    commission  = g.Sum(o => o.CommissionAmount),
                    netRevenue  = g.Sum(o => o.SellerPayoutAmount)
                })
                .ToDictionary(x => x.date);

            var result = new List<object>();
            for (var d = dateFrom; d < dateTo; d = d.AddDays(1))
            {
                var key = d.ToString("yyyy-MM-dd");
                if (grouped.TryGetValue(key, out var match))
                    result.Add(match);
                else
                    result.Add(new { date = key, totalOrders = 0, revenue = 0m, commission = 0m, netRevenue = 0m });
            }

            return Ok(result);
        }

        // GET /api/reports/seller/products?limit=10&from=&to=
        [HttpGet("products")]
        public async Task<IActionResult> GetTopProducts(
            [FromQuery] int limit    = 10,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to   = null)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var dateFrom = (from ?? DateTime.UtcNow.AddDays(-30)).Date;
            var dateTo   = (to   ?? DateTime.UtcNow).Date.AddDays(1);

            var productIds = await _db.Products
                .Where(p => p.ShopId == shop!.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var orderDetails = await _db.OrderDetails
                .Include(d => d.Product).ThenInclude(p => p!.Category)
                .Include(d => d.Order)
                .Where(d => productIds.Contains(d.ProductId)
                    && d.Order.OrderDate >= dateFrom && d.Order.OrderDate < dateTo
                    && d.Order.Status != OrderStatus.Cancelled)
                .ToListAsync();

            var avgRatings = await _db.Reviews
                .Where(r => productIds.Contains(r.ProductId))
                .GroupBy(r => r.ProductId)
                .Select(g => new { productId = g.Key, avg = g.Average(r => (double)r.Rating) })
                .ToDictionaryAsync(x => x.productId, x => x.avg);

            var topProducts = orderDetails
                .GroupBy(d => d.ProductId)
                .Select(g =>
                {
                    var p = g.First().Product;
                    avgRatings.TryGetValue(g.Key, out var ar);
                    return new {
                        productId = g.Key,
                        name      = p?.Name ?? "",
                        image     = p?.ImageUrl ?? "",
                        category  = p?.Category?.Name ?? "",
                        soldCount = g.Sum(d => d.Quantity),
                        revenue   = g.Sum(d => d.UnitPrice * d.Quantity),
                        avgRating = Math.Round(ar, 1)
                    };
                })
                .OrderByDescending(x => x.soldCount)
                .Take(limit)
                .ToList();

            return Ok(topProducts);
        }

        // GET /api/reports/seller/summary
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productCount = await _db.Products.CountAsync(p => p.ShopId == shop!.Id);

            var completedOrders = await _db.Orders
                .Where(o => o.ShopId == shop!.Id && o.Status == OrderStatus.Completed)
                .ToListAsync();

            var totalRevenue    = completedOrders.Sum(o => o.ProductRevenue);
            var totalCommission = completedOrders.Sum(o => o.CommissionAmount);
            var totalNet        = completedOrders.Sum(o => o.SellerPayoutAmount);
            var orderCount      = completedOrders.Count;
            var avgOrderValue   = orderCount > 0 ? Math.Round(totalRevenue / orderCount, 0) : 0m;

            var wallet = await _db.SellerWallets.FindAsync(shop!.Id);

            var topCategoryData = await _db.OrderDetails
                .Include(d => d.Product).ThenInclude(p => p!.Category)
                .Where(d => d.Product!.ShopId == shop.Id
                    && d.Order.Status == OrderStatus.Completed)
                .GroupBy(d => d.Product!.Category!.Name)
                .Select(g => new { category = g.Key, rev = g.Sum(d => d.UnitPrice * d.Quantity) })
                .OrderByDescending(x => x.rev)
                .FirstOrDefaultAsync();

            return Ok(new {
                totalRevenue,
                totalOrders     = orderCount,
                totalProducts   = productCount,
                avgOrderValue,
                totalCommission,
                totalNetRevenue = totalNet,
                topCategory     = topCategoryData?.category ?? "—",
                walletBalance   = wallet?.Balance ?? 0m,
                walletPending   = await _db.Orders
                    .Where(o => o.ShopId == shop.Id && o.PayoutStatus == PayoutStatusValue.WaitingRelease)
                    .SumAsync(o => o.SellerPayoutAmount)
            });
        }
    }
}
