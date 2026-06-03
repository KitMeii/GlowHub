using BaseCore.Common;
using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Repository.EFCore;
using BaseCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    // ─── Seller Wallet ────────────────────────────────────────────
    [Route("api/seller/wallet")]
    [ApiController]
    [Authorize(Roles = RoleConstant.Seller)]
    public class SellerWalletController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly IShopRepositoryEF _shopRepository;

        public SellerWalletController(MySqlDbContext db, IShopRepositoryEF shopRepository)
        {
            _db             = db;
            _shopRepository = shopRepository;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<(Shop? shop, IActionResult? error)> GetActiveShopAsync()
        {
            var shop = await _shopRepository.GetBySellerIdAsync(GetUserId()!);
            if (shop == null) return (null, NotFound(new { message = "Bạn chưa có shop" }));
            if (shop.Status != ShopStatus.Active) return (null, BadRequest(new { message = "Shop chưa được duyệt" }));
            return (shop, null);
        }

        private async Task<SellerWallet> GetOrCreateWalletAsync(string shopId)
        {
            var wallet = await _db.SellerWallets.FindAsync(shopId);
            if (wallet == null)
            {
                wallet = new SellerWallet { ShopId = shopId };
                _db.SellerWallets.Add(wallet);
                await _db.SaveChangesAsync();
            }
            return wallet;
        }

        /// <summary>GET /api/seller/wallet — Số dư ví + tổng hợp tài chính</summary>
        [HttpGet]
        public async Task<IActionResult> GetWallet()
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var wallet = await GetOrCreateWalletAsync(shop!.Id);

            var pendingPayout = await _db.SubOrders
                .Where(s => s.ShopId == shop.Id && s.PayoutStatus == PayoutStatusValue.WaitingRelease)
                .SumAsync(s => s.SellerPayoutAmount);

            var waitingCount = await _db.SubOrders
                .CountAsync(s => s.ShopId == shop.Id && s.PayoutStatus == PayoutStatusValue.WaitingRelease);

            return Ok(new {
                shopId         = shop.Id,
                shopName       = shop.ShopName,
                balance        = wallet.Balance,
                totalEarned    = wallet.TotalEarned,
                totalWithdrawn = wallet.TotalWithdrawn,
                totalRefunded  = wallet.TotalRefunded,
                pendingPayout,
                waitingCount,
                updatedAt      = wallet.UpdatedAt
            });
        }

        /// <summary>GET /api/seller/wallet/transactions?type=&page=&limit=</summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] string? type = null,
            [FromQuery] int page     = 1,
            [FromQuery] int limit    = 20)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var query = _db.WalletTransactions
                .Where(t => t.ShopId == shop!.Id)
                .AsQueryable();

            if (!string.IsNullOrEmpty(type))
                query = query.Where(t => t.Type == type.ToUpper());

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(t => new {
                    t.Id, t.Type, t.Amount,
                    t.BalanceBefore, t.BalanceAfter,
                    t.Note, t.OrderId, t.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }
    }

    // ─── Admin Wallet ─────────────────────────────────────────────
    [Route("api/admin/wallet")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminWalletController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;

        public AdminWalletController(MySqlDbContext db, AuditLogService audit)
        {
            _db    = db;
            _audit = audit;
        }

        private string? GetUserId()   => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

        private async Task<SellerWallet> GetOrCreateWalletAsync(string shopId)
        {
            var wallet = await _db.SellerWallets.FindAsync(shopId);
            if (wallet == null)
            {
                wallet = new SellerWallet { ShopId = shopId };
                _db.SellerWallets.Add(wallet);
                await _db.SaveChangesAsync();
            }
            return wallet;
        }

        /// <summary>GET /api/admin/wallet/overview — Tổng quan ví tất cả shop</summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            var wallets = await _db.SellerWallets
                .Include(w => w.Shop)
                .OrderByDescending(w => w.Balance)
                .Select(w => new {
                    shopId         = w.ShopId,
                    shopName       = w.Shop != null ? w.Shop.ShopName : "",
                    logo           = w.Shop != null ? w.Shop.Logo : null,
                    balance        = w.Balance,
                    totalEarned    = w.TotalEarned,
                    totalWithdrawn = w.TotalWithdrawn,
                    totalRefunded  = w.TotalRefunded,
                    updatedAt      = w.UpdatedAt
                })
                .ToListAsync();

            var pendingCount  = await _db.SubOrders.CountAsync(s => s.PayoutStatus == PayoutStatusValue.WaitingRelease);
            var pendingAmount = await _db.SubOrders
                .Where(s => s.PayoutStatus == PayoutStatusValue.WaitingRelease)
                .SumAsync(s => s.SellerPayoutAmount);

            return Ok(new {
                wallets,
                pendingReleaseCount  = pendingCount,
                pendingReleaseAmount = pendingAmount,
                totalBalance         = wallets.Sum(w => w.balance),
                totalEarned          = wallets.Sum(w => w.totalEarned),
                totalWithdrawn       = wallets.Sum(w => w.totalWithdrawn)
            });
        }

        /// <summary>POST /api/admin/wallet/release-payouts — Giải ngân WAITING_RELEASE → RELEASED (SubOrder-based)</summary>
        [HttpPost("release-payouts")]
        public async Task<IActionResult> ReleasePayouts()
        {
            var subOrders = await _db.SubOrders
                .Where(s => s.PayoutStatus == PayoutStatusValue.WaitingRelease && s.ShopId != null)
                .ToListAsync();

            if (!subOrders.Any())
                return Ok(new { message = "Không có đơn nào đang chờ giải ngân", released = 0, amount = 0m });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                int     releasedCount  = 0;
                decimal releasedAmount = 0m;

                // Group by shop and credit each wallet once per batch
                foreach (var shopGroup in subOrders.GroupBy(s => s.ShopId!))
                {
                    var wallet         = await GetOrCreateWalletAsync(shopGroup.Key);
                    var runningBalance = wallet.Balance;

                    foreach (var sub in shopGroup)
                    {
                        var before = runningBalance;
                        var after  = before + sub.SellerPayoutAmount;

                        _db.WalletTransactions.Add(new WalletTransaction {
                            ShopId        = sub.ShopId,
                            OrderId       = sub.OrderId,
                            Type          = WalletTransactionType.Earning,
                            Amount        = sub.SellerPayoutAmount,
                            BalanceBefore = before,
                            BalanceAfter  = after,
                            Note          = $"Giải ngân {sub.SubOrderCode ?? ("SUB-" + sub.Id.ToString("D6"))}",
                            CreatedAt     = DateTime.UtcNow
                        });

                        sub.PayoutStatus = PayoutStatusValue.Released;
                        runningBalance   = after;
                        releasedCount++;
                        releasedAmount  += sub.SellerPayoutAmount;
                    }

                    wallet.Balance      = runningBalance;
                    wallet.TotalEarned += shopGroup.Sum(s => s.SellerPayoutAmount);
                    wallet.UpdatedAt    = DateTime.UtcNow;
                }

                // Mark parent Order Released when all its SubOrders are now Released
                var batchSubIds = subOrders.Select(s => s.Id).ToHashSet();
                foreach (var orderId in subOrders.Select(s => s.OrderId).Distinct())
                {
                    var allSubs     = await _db.SubOrders.Where(s => s.OrderId == orderId).ToListAsync();
                    var allReleased = allSubs.All(s => batchSubIds.Contains(s.Id) || s.PayoutStatus == PayoutStatusValue.Released);
                    if (allReleased)
                    {
                        var parentOrder = await _db.Orders.FindAsync(orderId);
                        if (parentOrder != null)
                        {
                            parentOrder.PayoutStatus    = PayoutStatusValue.Released;
                            parentOrder.WalletReleaseAt = DateTime.UtcNow;
                        }
                    }
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                await _audit.Log(GetUserId(), GetUserName(), "WALLET_RELEASE_BATCH",
                    "WalletTransactions", null, null,
                    new { releasedCount, releasedAmount });

                return Ok(new {
                    message  = $"Đã giải ngân {releasedCount} sub-đơn hàng",
                    released = releasedCount,
                    amount   = releasedAmount
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>GET /api/admin/wallet/transactions?shopId=&type=&page=&limit=</summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] string? shopId = null,
            [FromQuery] string? type   = null,
            [FromQuery] int page       = 1,
            [FromQuery] int limit      = 20)
        {
            var query = _db.WalletTransactions.Include(t => t.Shop).AsQueryable();

            if (!string.IsNullOrEmpty(shopId)) query = query.Where(t => t.ShopId == shopId);
            if (!string.IsNullOrEmpty(type))   query = query.Where(t => t.Type == type.ToUpper());

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(t => new {
                    t.Id, t.ShopId,
                    shopName = t.Shop != null ? t.Shop.ShopName : "",
                    t.OrderId, t.Type, t.Amount,
                    t.BalanceBefore, t.BalanceAfter,
                    t.Note, t.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }
    }
}
