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
            var completedOrders = await _db.Orders
                .Where(o => o.Status == OrderStatus.Completed && o.ShopId != null)
                .ToListAsync();

            var wallets = await _db.SellerWallets
                .Include(w => w.Shop)
                .ToListAsync();

            var totalRevenue    = completedOrders.Sum(o => o.ProductRevenue);
            var totalCommission = completedOrders.Sum(o => o.CommissionAmount);
            var totalNetSellers = completedOrders.Sum(o => o.SellerPayoutAmount);
            var totalPaidOut    = wallets.Sum(w => w.TotalWithdrawn);
            var totalReleased   = wallets.Sum(w => w.TotalEarned);

            var pendingRelease = await _db.Orders
                .Where(o => o.PayoutStatus == PayoutStatusValue.WaitingRelease)
                .SumAsync(o => o.SellerPayoutAmount);

            var byShop = completedOrders
                .Where(o => o.ShopId != null)
                .GroupBy(o => o.ShopId!)
                .Select(g => {
                    var wallet = wallets.FirstOrDefault(w => w.ShopId == g.Key);
                    return new {
                        shopId           = g.Key,
                        shopName         = wallet?.Shop?.ShopName ?? g.Key,
                        logo             = wallet?.Shop?.Logo,
                        commissionRate   = g.First().CommissionRate,
                        totalRevenue     = g.Sum(o => o.ProductRevenue),
                        commissionAmount = g.Sum(o => o.CommissionAmount),
                        netRevenue       = g.Sum(o => o.SellerPayoutAmount),
                        walletBalance    = wallet?.Balance ?? 0m,
                        totalEarned      = wallet?.TotalEarned ?? 0m,
                        totalWithdrawn   = wallet?.TotalWithdrawn ?? 0m
                    };
                })
                .OrderByDescending(x => x.totalRevenue)
                .Take(20);

            // Admin net profit: commission - systemVoucher - freeship
            var adminNetProfit = completedOrders.Sum(o =>
                o.CommissionAmount - o.SystemVoucherDiscount - o.FreeshipDiscount);

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

            var orders = await _db.Orders
                .Where(o => o.Status == OrderStatus.Completed
                         && o.ShopId != null
                         && shopIds.Contains(o.ShopId!)
                         && o.OrderDate >= fromDate && o.OrderDate < toDate)
                .ToListAsync();

            var wallets = await _db.SellerWallets
                .Where(w => shopIds.Contains(w.ShopId))
                .ToListAsync();

            var result = shops.Select(shop => {
                var shopOrders = orders.Where(o => o.ShopId == shop.Id).ToList();
                var wallet     = wallets.FirstOrDefault(w => w.ShopId == shop.Id);
                return new {
                    shopId           = shop.Id,
                    shopName         = shop.ShopName,
                    logo             = shop.Logo,
                    commissionRate   = shop.CommissionRate,
                    revenue          = shopOrders.Sum(o => o.ProductRevenue),
                    commissionAmount = shopOrders.Sum(o => o.CommissionAmount),
                    netRevenue       = shopOrders.Sum(o => o.SellerPayoutAmount),
                    walletBalance    = wallet?.Balance ?? 0m,
                    totalEarned      = wallet?.TotalEarned ?? 0m,
                    totalWithdrawn   = wallet?.TotalWithdrawn ?? 0m,
                    orderCount       = shopOrders.Count
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
