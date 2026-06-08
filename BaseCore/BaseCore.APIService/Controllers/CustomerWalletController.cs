using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/customer/wallet")]
    [ApiController]
    [Authorize]
    public class CustomerWalletController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public CustomerWalletController(MySqlDbContext db) => _db = db;

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<CustomerWallet> GetOrCreateWalletAsync(string userId)
        {
            var wallet = await _db.CustomerWallets.FindAsync(userId);
            if (wallet == null)
            {
                wallet = new CustomerWallet { UserId = userId, UpdatedAt = DateTime.UtcNow };
                _db.CustomerWallets.Add(wallet);
                await _db.SaveChangesAsync();
            }
            return wallet;
        }

        /// <summary>GET /api/customer/wallet — Số dư ví của khách</summary>
        [HttpGet]
        public async Task<IActionResult> GetWallet()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var wallet = await GetOrCreateWalletAsync(userId);

            return Ok(new {
                userId        = wallet.UserId,
                balance       = wallet.Balance,
                totalReceived = wallet.TotalReceived,
                totalSpent    = wallet.TotalSpent,
                updatedAt     = wallet.UpdatedAt
            });
        }

        /// <summary>GET /api/customer/wallet/transactions?type=&amp;page=&amp;limit=</summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] string? type = null,
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 20)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var query = _db.CustomerWalletTransactions
                .Where(t => t.UserId == userId)
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

        /// <summary>
        /// POST /api/customer/wallet/topup — Nạp tiền vào ví (giả lập — production sẽ qua payment gateway)
        /// Body: { amount }
        /// </summary>
        [HttpPost("topup")]
        public async Task<IActionResult> TopUp([FromBody] WalletTopUpDto dto)
        {
            if (dto.Amount <= 0)
                return BadRequest(new { message = "Số tiền nạp phải lớn hơn 0" });
            if (dto.Amount > 50_000_000m)
                return BadRequest(new { message = "Tối đa 50.000.000₫ mỗi lần nạp" });

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var wallet = await GetOrCreateWalletAsync(userId);
                var before = wallet.Balance;
                var after  = before + dto.Amount;

                wallet.Balance       = after;
                wallet.TotalReceived += dto.Amount;
                wallet.UpdatedAt     = DateTime.UtcNow;

                _db.CustomerWalletTransactions.Add(new CustomerWalletTransaction {
                    UserId        = userId,
                    Type          = CustomerWalletTxType.TopUp,
                    Amount        = dto.Amount,
                    BalanceBefore = before,
                    BalanceAfter  = after,
                    Note          = dto.Note ?? "Nạp tiền vào ví",
                    CreatedAt     = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new {
                    message      = "Nạp tiền thành công!",
                    balance      = wallet.Balance,
                    topUpAmount  = dto.Amount
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    // ─── Admin CustomerWallet ─────────────────────────────────
    [Route("api/admin/customer-wallet")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminCustomerWalletController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public AdminCustomerWalletController(MySqlDbContext db) => _db = db;

        /// <summary>GET /api/admin/customer-wallet/overview — Tổng quan ví tất cả khách</summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 20)
        {
            var total   = await _db.CustomerWallets.CountAsync();
            var wallets = await _db.CustomerWallets
                .Include(w => w.User)
                .OrderByDescending(w => w.Balance)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(w => new {
                    userId        = w.UserId,
                    userName      = w.User != null ? w.User.Name : "",
                    email         = w.User != null ? w.User.Email : "",
                    balance       = w.Balance,
                    totalReceived = w.TotalReceived,
                    totalSpent    = w.TotalSpent,
                    updatedAt     = w.UpdatedAt
                })
                .ToListAsync();

            return Ok(new {
                items      = wallets,
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit),
                totalBalance = await _db.CustomerWallets.SumAsync(w => w.Balance)
            });
        }

        /// <summary>POST /api/admin/customer-wallet/adjust — Admin điều chỉnh số dư</summary>
        [HttpPost("adjust")]
        public async Task<IActionResult> Adjust([FromBody] AdminWalletAdjustDto dto)
        {
            if (string.IsNullOrEmpty(dto.UserId))
                return BadRequest(new { message = "UserId là bắt buộc" });
            if (dto.Amount == 0)
                return BadRequest(new { message = "Số tiền không được bằng 0" });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var wallet = await _db.CustomerWallets.FindAsync(dto.UserId);
                if (wallet == null)
                {
                    wallet = new CustomerWallet { UserId = dto.UserId, UpdatedAt = DateTime.UtcNow };
                    _db.CustomerWallets.Add(wallet);
                    await _db.SaveChangesAsync();
                }

                var before = wallet.Balance;
                var after  = before + dto.Amount;
                if (after < 0) return BadRequest(new { message = "Số dư không đủ để trừ" });

                wallet.Balance    = after;
                wallet.UpdatedAt  = DateTime.UtcNow;
                if (dto.Amount > 0) wallet.TotalReceived += dto.Amount;

                _db.CustomerWalletTransactions.Add(new CustomerWalletTransaction {
                    UserId        = dto.UserId,
                    Type          = CustomerWalletTxType.Adjustment,
                    Amount        = dto.Amount,
                    BalanceBefore = before,
                    BalanceAfter  = after,
                    Note          = dto.Note ?? (dto.Amount > 0 ? "Admin cộng tiền" : "Admin trừ tiền"),
                    CreatedAt     = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new { message = "Điều chỉnh thành công", newBalance = after });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class WalletTopUpDto
    {
        public decimal Amount { get; set; }
        public string? Note   { get; set; }
    }

    public class AdminWalletAdjustDto
    {
        public string  UserId { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Note   { get; set; }
    }
}
