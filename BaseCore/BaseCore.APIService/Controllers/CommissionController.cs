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
            // SubOrder is source of truth for per-shop financials — aggregate directly in DB
            var subsQuery = _db.SubOrders.Where(s => s.Order.Status == OrderStatus.Completed);

            var totalRevenue    = await subsQuery.SumAsync(s => s.ProductRevenue);
            var totalCommission = await subsQuery.SumAsync(s => s.CommissionAmount);
            var totalNetSellers = await subsQuery.SumAsync(s => s.SellerPayoutAmount);

            var totalPaidOut  = await _db.SellerWallets.SumAsync(w => w.TotalWithdrawn);
            var totalReleased = await _db.SellerWallets.SumAsync(w => w.TotalEarned);

            var pendingRelease = await _db.SubOrders
                .Where(s => s.PayoutStatus == PayoutStatusValue.WaitingRelease)
                .SumAsync(s => s.SellerPayoutAmount);

            // Admin net profit = commission - system-level discounts (stored on Order)
            var systemDiscounts = await _db.Orders
                .Where(o => o.Status == OrderStatus.Completed)
                .SumAsync(o => o.SystemVoucherDiscount + o.FreeshipDiscount);
            var adminNetProfit = totalCommission - systemDiscounts;

            // byShop: GROUP BY in DB → join wallet info only for top 20 shops
            var byShopAgg = await subsQuery
                .Where(s => s.ShopId != null)
                .GroupBy(s => s.ShopId!)
                .Select(g => new {
                    shopId           = g.Key,
                    commissionRate   = g.Max(s => s.CommissionRate),
                    totalRevenue     = g.Sum(s => s.ProductRevenue),
                    commissionAmount = g.Sum(s => s.CommissionAmount),
                    netRevenue       = g.Sum(s => s.SellerPayoutAmount)
                })
                .OrderByDescending(x => x.totalRevenue)
                .Take(20)
                .ToListAsync();

            var topShopIds = byShopAgg.Select(x => x.shopId).ToList();
            var wallets = await _db.SellerWallets
                .Include(w => w.Shop)
                .Where(w => topShopIds.Contains(w.ShopId))
                .ToListAsync();
            var walletDict = wallets.ToDictionary(w => w.ShopId);

            var byShop = byShopAgg.Select(x => {
                walletDict.TryGetValue(x.shopId, out var wallet);
                return new {
                    shopId           = x.shopId,
                    shopName         = wallet?.Shop?.ShopName ?? x.shopId,
                    logo             = wallet?.Shop?.Logo,
                    x.commissionRate,
                    x.totalRevenue,
                    x.commissionAmount,
                    x.netRevenue,
                    walletBalance    = wallet?.Balance ?? 0m,
                    totalEarned      = wallet?.TotalEarned ?? 0m,
                    totalWithdrawn   = wallet?.TotalWithdrawn ?? 0m
                };
            });

            return Ok(new {
                totalRevenue,
                totalCommission,
                adminNetProfit,
                totalNetToSellers = totalNetSellers,
                totalReleased,
                totalPaidOut,
                pendingRelease,
                byShop
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
            var toDate   = DateTime.TryParse(to,   out var t) ? t.ToUniversalTime().Date.AddDays(1) : DateTime.UtcNow.Date.AddDays(1);

            var shopQuery = _db.Shops.AsQueryable();
            if (!string.IsNullOrEmpty(shopId)) shopQuery = shopQuery.Where(s => s.Id == shopId);

            var total = await shopQuery.CountAsync();
            var shops = await shopQuery
                .OrderBy(s => s.ShopName)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var shopIds = shops.Select(s => s.Id).ToList();

            // Use SubOrders as source of truth for per-shop financials
            var subOrders = await _db.SubOrders
                .Include(s => s.Order)
                .Where(s => shopIds.Contains(s.ShopId!)
                         && s.Order.OrderDate >= fromDate && s.Order.OrderDate < toDate
                         && s.Order.Status != OrderStatus.Cancelled)
                .ToListAsync();

            var wallets = await _db.SellerWallets
                .Where(w => shopIds.Contains(w.ShopId))
                .ToListAsync();

            // Pending payout per shop (WaitingRelease SubOrders)
            var pendingMap = await _db.SubOrders
                .Where(s => shopIds.Contains(s.ShopId!) && s.PayoutStatus == PayoutStatusValue.WaitingRelease)
                .GroupBy(s => s.ShopId!)
                .Select(g => new { ShopId = g.Key, Amount = g.Sum(s => s.SellerPayoutAmount) })
                .ToListAsync();
            var pendingByShop = pendingMap.ToDictionary(x => x.ShopId, x => x.Amount);

            var result = shops.Select(shop => {
                var shopSubs = subOrders.Where(s => s.ShopId == shop.Id).ToList();
                var wallet   = wallets.FirstOrDefault(w => w.ShopId == shop.Id);
                return new {
                    shopId           = shop.Id,
                    shopName         = shop.ShopName,
                    logo             = shop.Logo,
                    commissionRate   = shop.CommissionRate,
                    revenue          = shopSubs.Sum(s => s.ProductRevenue),
                    commissionAmount = shopSubs.Sum(s => s.CommissionAmount),
                    netRevenue       = shopSubs.Sum(s => s.SellerPayoutAmount),
                    walletBalance    = wallet?.Balance ?? 0m,
                    totalEarned      = wallet?.TotalEarned ?? 0m,
                    totalWithdrawn   = wallet?.TotalWithdrawn ?? 0m,
                    pendingPayout    = pendingByShop.TryGetValue(shop.Id, out var pp) ? pp : 0m,
                    orderCount       = shopSubs.Select(s => s.OrderId).Distinct().Count()
                };
            }).ToList();

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
