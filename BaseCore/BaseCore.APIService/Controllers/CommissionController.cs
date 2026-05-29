using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/admin/commission")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class CommissionController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;

        public CommissionController(MySqlDbContext db, AuditLogService audit)
        {
            _db    = db;
            _audit = audit;
        }

        private string? GetUserId()   => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

        // ─────────────────────────────────────────────────────────
        // GET /api/admin/commission/summary
        // ─────────────────────────────────────────────────────────
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var shops = await _db.Shops
                .Where(s => s.Status == ShopStatus.Active)
                .ToListAsync();

            var allOrders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.Status == OrderStatus.Completed)
                .ToListAsync();

            var payouts = await _db.PayoutHistories.ToListAsync();

            decimal totalRevenue   = 0;
            decimal totalCommission = 0;
            decimal totalPaidOut   = 0;
            var byShop = new List<object>();

            foreach (var shop in shops)
            {
                var productIds = await _db.Products
                    .Where(p => p.ShopId == shop.Id)
                    .Select(p => p.Id)
                    .ToListAsync();

                var shopRevenue = allOrders
                    .Where(o => o.OrderDetails.Any(od => productIds.Contains(od.ProductId)))
                    .Sum(o => o.OrderDetails
                        .Where(od => productIds.Contains(od.ProductId))
                        .Sum(od => od.UnitPrice * od.Quantity));

                var commission = shopRevenue * (shop.CommissionRate / 100m);
                var paidOut    = payouts.Where(p => p.ShopId == shop.Id).Sum(p => p.Amount);

                totalRevenue    += shopRevenue;
                totalCommission += commission;
                totalPaidOut    += paidOut;

                byShop.Add(new {
                    shopId          = shop.Id,
                    shopName        = shop.ShopName,
                    logo            = shop.Logo,
                    commissionRate  = shop.CommissionRate,
                    totalRevenue    = shopRevenue,
                    commissionAmount = commission,
                    netRevenue      = shopRevenue - commission,
                    paidOut,
                    pendingPayout   = commission - paidOut
                });
            }

            return Ok(new {
                totalRevenue,
                totalCommission,
                totalNetToSellers = totalRevenue - totalCommission,
                totalPaidOut,
                pendingTotal = totalCommission - totalPaidOut,
                byShop = byShop.OrderByDescending(x => ((dynamic)x).totalRevenue).Take(20)
            });
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/admin/commission/shops?from=&to=&shopId=&page=&limit=
        // ─────────────────────────────────────────────────────────
        [HttpGet("shops")]
        public async Task<IActionResult> GetShops(
            [FromQuery] string? from   = null,
            [FromQuery] string? to     = null,
            [FromQuery] string? shopId = null,
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 20)
        {
            var fromDate = DateTime.TryParse(from, out var f) ? f.ToUniversalTime().Date : DateTime.UtcNow.AddDays(-29).Date;
            var toDate   = DateTime.TryParse(to,   out var t) ? t.ToUniversalTime().Date : DateTime.UtcNow.Date;

            var shopQuery = _db.Shops.AsQueryable();
            if (!string.IsNullOrEmpty(shopId)) shopQuery = shopQuery.Where(s => s.Id == shopId);

            var total = await shopQuery.CountAsync();
            var shops = await shopQuery
                .OrderBy(s => s.ShopName)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.Status == OrderStatus.Completed
                         && o.OrderDate.Date >= fromDate
                         && o.OrderDate.Date <= toDate)
                .ToListAsync();

            var payouts = await _db.PayoutHistories
                .Where(p => (string.IsNullOrEmpty(shopId) || p.ShopId == shopId))
                .ToListAsync();

            var result = new List<object>();
            foreach (var shop in shops)
            {
                var productIds = await _db.Products
                    .Where(p => p.ShopId == shop.Id)
                    .Select(p => p.Id)
                    .ToListAsync();

                var revenue = orders
                    .Where(o => o.OrderDetails.Any(od => productIds.Contains(od.ProductId)))
                    .Sum(o => o.OrderDetails
                        .Where(od => productIds.Contains(od.ProductId))
                        .Sum(od => od.UnitPrice * od.Quantity));

                var commission = revenue * (shop.CommissionRate / 100m);
                var paidOut    = payouts.Where(p => p.ShopId == shop.Id).Sum(p => p.Amount);

                result.Add(new {
                    shopId         = shop.Id,
                    shopName       = shop.ShopName,
                    logo           = shop.Logo,
                    commissionRate = shop.CommissionRate,
                    revenue,
                    commissionAmount = commission,
                    netRevenue     = revenue - commission,
                    paidOut,
                    pendingPayout  = commission - paidOut
                });
            }

            return Ok(new { items = result, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        // ─────────────────────────────────────────────────────────
        // POST /api/admin/commission/payout/{shopId}
        // ─────────────────────────────────────────────────────────
        [HttpPost("payout/{shopId}")]
        public async Task<IActionResult> CreatePayout(string shopId, [FromBody] CreatePayoutDto dto)
        {
            var shop = await _db.Shops.FindAsync(shopId);
            if (shop == null) return NotFound(new { message = "Không tìm thấy shop" });

            if (dto.Amount <= 0)
                return BadRequest(new { message = "Số tiền thanh toán phải lớn hơn 0" });

            var payout = new PayoutHistory
            {
                ShopId      = shopId,
                Amount      = dto.Amount,
                Note        = dto.Note,
                PayoutDate  = dto.PayoutDate == default ? DateTime.UtcNow : dto.PayoutDate,
                ProcessedBy = GetUserId(),
                CreatedAt   = DateTime.UtcNow
            };

            _db.PayoutHistories.Add(payout);
            await _db.SaveChangesAsync();

            await _audit.Log(GetUserId(), GetUserName(), "COMMISSION_PAYOUT",
                "Shop", shopId,
                null,
                new { amount = dto.Amount, note = dto.Note, payoutDate = payout.PayoutDate });

            return Ok(new {
                message    = $"Đã ghi nhận thanh toán {dto.Amount:N0}₫ cho {shop.ShopName}",
                payoutId   = payout.Id,
                shopName   = shop.ShopName,
                amount     = payout.Amount,
                payoutDate = payout.PayoutDate
            });
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/admin/commission/history?shopId=&from=&to=&page=&limit=
        // ─────────────────────────────────────────────────────────
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] string? shopId = null,
            [FromQuery] string? from   = null,
            [FromQuery] string? to     = null,
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 20)
        {
            var fromDate = DateTime.TryParse(from, out var f) ? f.ToUniversalTime() : DateTime.UtcNow.AddDays(-89);
            var toDate   = DateTime.TryParse(to,   out var t) ? t.ToUniversalTime().AddDays(1) : DateTime.UtcNow.AddDays(1);

            var query = _db.PayoutHistories
                .Include(p => p.Shop)
                .Where(p => p.PayoutDate >= fromDate && p.PayoutDate <= toDate);

            if (!string.IsNullOrEmpty(shopId))
                query = query.Where(p => p.ShopId == shopId);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.PayoutDate)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(p => new {
                    p.Id,
                    p.ShopId,
                    shopName    = p.Shop != null ? p.Shop.ShopName : "",
                    p.Amount,
                    p.Note,
                    p.PayoutDate,
                    p.ProcessedBy,
                    p.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }
    }

    public class CreatePayoutDto
    {
        public decimal Amount { get; set; }
        public string? Note { get; set; }
        public DateTime PayoutDate { get; set; }
    }
}
