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

            var productIds = await _db.Products
                .Where(p => p.ShopId == shop!.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var commissionRate = ((shop!.CommissionRate > 0 ? shop.CommissionRate : 10m)) / 100m;

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.OrderDate >= dateFrom && o.OrderDate < dateTo
                    && o.Status != OrderStatus.Cancelled
                    && o.OrderDetails.Any(d => productIds.Contains(d.ProductId)))
                .ToListAsync();

            var grouped = orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g =>
                {
                    var rev = g.Sum(o =>
                        o.OrderDetails.Where(d => productIds.Contains(d.ProductId))
                                      .Sum(d => d.UnitPrice * d.Quantity));
                    var comm = Math.Round(rev * commissionRate, 0);
                    return new { date = g.Key.ToString("yyyy-MM-dd"), totalOrders = g.Count(), revenue = rev, commission = comm, netRevenue = rev - comm };
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

            var productIds = await _db.Products
                .Where(p => p.ShopId == shop!.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var commissionRate = ((shop!.CommissionRate > 0 ? shop.CommissionRate : 10m)) / 100m;

            var completedDetails = await _db.OrderDetails
                .Include(d => d.Order)
                .Include(d => d.Product).ThenInclude(p => p!.Category)
                .Where(d => productIds.Contains(d.ProductId) && d.Order.Status == OrderStatus.Completed)
                .ToListAsync();

            var totalRevenue    = completedDetails.Sum(d => d.UnitPrice * d.Quantity);
            var totalCommission = Math.Round(totalRevenue * commissionRate, 0);
            var orderCount      = completedDetails.Select(d => d.OrderId).Distinct().Count();
            var avgOrderValue   = orderCount > 0 ? Math.Round(totalRevenue / orderCount, 0) : 0m;

            var topCategory = completedDetails
                .Where(d => d.Product?.Category != null)
                .GroupBy(d => d.Product!.Category!.Name)
                .OrderByDescending(g => g.Sum(d => d.UnitPrice * d.Quantity))
                .Select(g => g.Key)
                .FirstOrDefault() ?? "—";

            return Ok(new {
                totalRevenue,
                totalOrders     = orderCount,
                totalProducts   = productIds.Count,
                avgOrderValue,
                totalCommission,
                totalNetRevenue = totalRevenue - totalCommission,
                topCategory
            });
        }
    }
}
