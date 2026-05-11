using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VouchersController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public VouchersController(MySqlDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// POST /api/Vouchers/validate — Kiểm tra mã giảm giá (user)
        /// Body: { "code": "GLOW10", "orderAmount": 500000 }
        /// Response: { valid, discountAmount, message }
        /// </summary>
        [HttpPost("validate")]
        [Authorize]
        public async Task<IActionResult> Validate([FromBody] ValidateVoucherDto dto)
        {
            var voucher = await _db.Vouchers
                .FirstOrDefaultAsync(v => v.Code == dto.Code.ToUpper() && v.IsActive);

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

        // ── Admin endpoints ──

        /// <summary>GET /api/Vouchers (admin only)</summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _db.Vouchers.OrderByDescending(v => v.CreatedAt).ToListAsync());
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
            return CreatedAtAction(nameof(GetAll), voucher);
        }

        /// <summary>PUT /api/Vouchers/{id} (admin only)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] Voucher dto)
        {
            var v = await _db.Vouchers.FindAsync(id);
            if (v == null) return NotFound();
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
            return Ok(v);
        }

        /// <summary>DELETE /api/Vouchers/{id} (admin only)</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var v = await _db.Vouchers.FindAsync(id);
            if (v == null) return NotFound();
            _db.Vouchers.Remove(v);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa voucher" });
        }
    }

    public class ValidateVoucherDto
    {
        public string Code { get; set; } = "";
        public decimal OrderAmount { get; set; }
    }
}