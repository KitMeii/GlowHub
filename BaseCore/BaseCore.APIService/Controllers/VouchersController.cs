using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VouchersController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;

        public VouchersController(MySqlDbContext db, AuditLogService audit)
        {
            _db    = db;
            _audit = audit;
        }

        private string? GetUserId()   => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

        /// <summary>GET /api/vouchers/public — Danh sách voucher public (không cần auth)</summary>
        [HttpGet("public")]
        public async Task<IActionResult> GetPublic()
        {
            var now = DateTime.UtcNow;
            var vouchers = await _db.Vouchers
                .Include(v => v.Shop)
                .Where(v => v.IsActive &&
                    (!v.ExpiryDate.HasValue || v.ExpiryDate >= now) &&
                    (!v.StartDate.HasValue  || v.StartDate  <= now) &&
                    (!v.UsageLimit.HasValue || v.UsedCount < v.UsageLimit))
                .OrderByDescending(v => v.DiscountValue)
                .Select(v => new {
                    v.Id,
                    v.Code,
                    v.Description,
                    v.DiscountType,
                    v.DiscountValue,
                    v.MaxDiscount,
                    v.MinOrderAmount,
                    v.ExpiryDate,
                    v.StartDate,
                    v.ShopId,
                    shopName = v.Shop != null ? v.Shop.ShopName : null,
                    remainingUsage = v.UsageLimit.HasValue ? v.UsageLimit - v.UsedCount : (int?)null,
                    voucherType = v.ShopId == null ? "system" : "shop"
                })
                .ToListAsync();

            return Ok(vouchers);
        }

        /// <summary>GET /api/vouchers/my — Voucher đã lưu của customer</summary>
        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMy()
        {
            var userId = GetUserId();
            var now = DateTime.UtcNow;
            var saved = await _db.CustomerVouchers
                .Include(cv => cv.Voucher)
                .Where(cv => cv.UserId == userId && !cv.IsUsed && cv.Voucher.IsActive)
                .OrderByDescending(cv => cv.SavedAt)
                .Select(cv => new {
                    cv.Id,
                    cv.SavedAt,
                    cv.IsUsed,
                    voucher = new {
                        cv.Voucher.Id,
                        cv.Voucher.Code,
                        cv.Voucher.Description,
                        cv.Voucher.DiscountType,
                        cv.Voucher.DiscountValue,
                        cv.Voucher.MaxDiscount,
                        cv.Voucher.MinOrderAmount,
                        cv.Voucher.ExpiryDate,
                        cv.Voucher.ShopId,
                        isExpired = cv.Voucher.ExpiryDate.HasValue && cv.Voucher.ExpiryDate < now
                    }
                })
                .ToListAsync();

            return Ok(saved);
        }

        /// <summary>POST /api/vouchers/save/{code} — Lưu voucher vào tài khoản</summary>
        [HttpPost("save/{code}")]
        [Authorize]
        public async Task<IActionResult> Save(string code)
        {
            var userId = GetUserId()!;
            var now = DateTime.UtcNow;

            var voucher = await _db.Vouchers.FirstOrDefaultAsync(v =>
                v.Code == code.ToUpper() && v.IsActive &&
                (!v.ExpiryDate.HasValue || v.ExpiryDate >= now));

            if (voucher == null)
                return NotFound(new { message = "Voucher không tồn tại hoặc đã hết hạn" });

            var already = await _db.CustomerVouchers
                .AnyAsync(cv => cv.UserId == userId && cv.VoucherId == voucher.Id);

            if (already)
                return BadRequest(new { message = "Bạn đã lưu voucher này rồi" });

            _db.CustomerVouchers.Add(new CustomerVoucher
            {
                UserId = userId,
                VoucherId = voucher.Id,
                SavedAt = now
            });
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã lưu voucher thành công", code = voucher.Code });
        }

        /// <summary>DELETE /api/vouchers/unsave/{code} — Xóa voucher đã lưu khỏi tài khoản</summary>
        [HttpDelete("unsave/{code}")]
        [Authorize]
        public async Task<IActionResult> Unsave(string code)
        {
            var userId = GetUserId()!;

            var cv = await _db.CustomerVouchers
                .Include(x => x.Voucher)
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Voucher.Code == code.ToUpper());

            if (cv == null)
                return NotFound(new { message = "Không tìm thấy voucher đã lưu" });

            _db.CustomerVouchers.Remove(cv);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã xóa voucher khỏi danh sách" });
        }

        /// <summary>
        /// POST /api/Vouchers/validate — Kiểm tra mã giảm giá (user)
        /// Body: { "code": "GLOW10", "orderAmount": 500000, "shopId": "..." }
        /// Response: { valid, discountAmount, message }
        /// </summary>
        [HttpPost("validate")]
        [Authorize]
        public async Task<IActionResult> Validate([FromBody] ValidateVoucherDto dto)
        {
            var voucher = await _db.Vouchers.FirstOrDefaultAsync(v =>
                v.Code == dto.Code.ToUpper() && v.IsActive &&
                (v.ShopId == null || v.ShopId == dto.ShopId));

            if (voucher == null)
                return Ok(new { valid = false, message = "Mã giảm giá không tồn tại hoặc đã hết hạn" });

            // Kiểm tra thời hạn
            if (voucher.ExpiryDate.HasValue && voucher.ExpiryDate < DateTime.UtcNow)
                return Ok(new { valid = false, message = "Mã giảm giá đã hết hạn" });

            if (voucher.StartDate.HasValue && voucher.StartDate > DateTime.UtcNow)
                return Ok(new { valid = false, message = "Mã giảm giá chưa có hiệu lực" });

            // Kiểm tra giới hạn sử dụng
            if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit)
                return Ok(new { valid = false, message = "Mã giảm giá đã hết lượt sử dụng" });

            // Kiểm tra đơn tối thiểu
            if (dto.OrderAmount < voucher.MinOrderAmount)
                return Ok(new
                {
                    valid = false,
                    message = $"Đơn hàng tối thiểu {voucher.MinOrderAmount:N0}₫ để dùng mã này"
                });

            // Tính số tiền giảm
            decimal discount = voucher.DiscountType == "percent"
                ? dto.OrderAmount * voucher.DiscountValue / 100
                : voucher.DiscountValue;

            if (voucher.MaxDiscount.HasValue && discount > voucher.MaxDiscount)
                discount = voucher.MaxDiscount.Value;

            return Ok(new
            {
                valid = true,
                discountAmount = discount,
                discountType = voucher.DiscountType,
                discountValue = voucher.DiscountValue,
                message = $"Áp dụng thành công! Giảm {discount:N0}₫"
            });
        }

        /// <summary>
        /// GET /api/Vouchers/available?shopId=&amp;orderAmount= — lấy voucher áp dụng được cho đơn hàng
        /// </summary>
        [HttpGet("available")]
        [Authorize]
        public async Task<IActionResult> GetAvailable(
            [FromQuery] string? shopId = null,
            [FromQuery] decimal orderAmount = 0)
        {
            var now = DateTime.UtcNow;
            var vouchers = await _db.Vouchers
                .Where(v => v.IsActive &&
                    (v.ShopId == null || v.ShopId == shopId) &&
                    (!v.ExpiryDate.HasValue || v.ExpiryDate >= now) &&
                    (!v.StartDate.HasValue  || v.StartDate  <= now) &&
                    (!v.UsageLimit.HasValue || v.UsedCount < v.UsageLimit) &&
                    v.MinOrderAmount <= orderAmount)
                .OrderByDescending(v => v.DiscountValue)
                .Select(v => new {
                    v.Id,
                    v.Code,
                    v.Description,
                    v.DiscountType,
                    v.DiscountValue,
                    v.MaxDiscount,
                    v.MinOrderAmount,
                    v.ExpiryDate,
                    v.ShopId
                })
                .ToListAsync();

            return Ok(vouchers);
        }

        // ── Admin endpoints ──

        /// <summary>GET /api/Vouchers (admin only) — paginated</summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 20)
        {
            var query = _db.Vouchers.OrderByDescending(v => v.CreatedAt);
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();
            return Ok(new { items, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        /// <summary>POST /api/Vouchers (admin only)</summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] Voucher voucher)
        {
            voucher.Code = voucher.Code.ToUpper().Trim();
            voucher.UsedCount = 0;
            voucher.CreatedAt = DateTime.UtcNow;
            _db.Vouchers.Add(voucher);
            await _db.SaveChangesAsync();
            await _audit.Log(GetUserId(), GetUserName(), "VOUCHER_CREATE", "Voucher", voucher.Id.ToString(),
                null, new { code = voucher.Code, discountValue = voucher.DiscountValue });
            return CreatedAtAction(nameof(GetAll), voucher);
        }

        /// <summary>PUT /api/Vouchers/{id} (admin only)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Voucher dto)
        {
            var v = await _db.Vouchers.FindAsync(id);
            if (v == null) return NotFound();
            var oldCode = v.Code;
            v.Description = dto.Description;
            v.DiscountType = dto.DiscountType;
            v.DiscountValue = dto.DiscountValue;
            v.MinOrderAmount = dto.MinOrderAmount;
            v.MaxDiscount = dto.MaxDiscount;
            v.UsageLimit = dto.UsageLimit;
            v.StartDate = dto.StartDate;
            v.ExpiryDate = dto.ExpiryDate;
            v.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();
            await _audit.Log(GetUserId(), GetUserName(), "VOUCHER_UPDATE", "Voucher", id.ToString(),
                new { code = oldCode }, new { code = v.Code, isActive = v.IsActive, discountValue = v.DiscountValue });
            return Ok(v);
        }

        /// <summary>DELETE /api/Vouchers/{id} (admin only)</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var v = await _db.Vouchers.FindAsync(id);
            if (v == null) return NotFound();
            await _audit.Log(GetUserId(), GetUserName(), "VOUCHER_DELETE", "Voucher", id.ToString(),
                new { code = v.Code }, null);
            _db.Vouchers.Remove(v);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa voucher" });
        }

        /// <summary>GET /api/Vouchers/{id}/usage — Lịch sử sử dụng voucher (Admin)</summary>
        [HttpGet("{id}/usage")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUsage(int id, [FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            var v = await _db.Vouchers.FindAsync(id);
            if (v == null) return NotFound(new { message = "Không tìm thấy voucher" });

            var query = _db.CustomerVouchers
                .Include(cv => cv.User)
                .Where(cv => cv.VoucherId == id);

            var total = await query.CountAsync();
            var usage = await query
                .OrderByDescending(cv => cv.SavedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(cv => new {
                    cv.Id,
                    cv.SavedAt,
                    cv.IsUsed,
                    user = new { cv.User.Id, cv.User.Name, cv.User.Email }
                })
                .ToListAsync();

            return Ok(new {
                voucherId = id,
                code      = v.Code,
                usedCount = v.UsedCount,
                items     = usage,
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit)
            });
        }
    }

    public class ValidateVoucherDto
    {
        public string Code { get; set; } = "";
        public decimal OrderAmount { get; set; }
        public string? ShopId { get; set; }
    }
}