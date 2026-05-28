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
    [Route("api/seller/vouchers")]
    [ApiController]
    [Authorize(Roles = RoleConstant.Seller)]
    public class SellerVouchersController : ControllerBase
    {
        private readonly IShopRepositoryEF _shopRepository;
        private readonly MySqlDbContext _db;

        public SellerVouchersController(IShopRepositoryEF shopRepository, MySqlDbContext db)
        {
            _shopRepository = shopRepository;
            _db = db;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<(Shop? shop, IActionResult? error)> GetActiveShopAsync()
        {
            var shop = await _shopRepository.GetBySellerIdAsync(GetUserId()!);
            if (shop == null) return (null, NotFound(new { message = "Bạn chưa có shop" }));
            if (shop.Status != ShopStatus.Active) return (null, BadRequest(new { message = "Shop chưa được duyệt" }));
            return (shop, null);
        }

        // GET /api/seller/vouchers
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var vouchers = await _db.Vouchers
                .Where(v => v.ShopId == shop!.Id)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var result = vouchers.Select(v => new
            {
                v.Id, v.Code, v.DiscountType, v.DiscountValue,
                v.MinOrderAmount, v.UsedCount,
                maxUsage  = v.UsageLimit,
                v.StartDate, v.ExpiryDate, v.IsActive,
                statusLabel = !v.IsActive ? "inactive"
                    : v.ExpiryDate.HasValue && v.ExpiryDate < now ? "expired"
                    : v.UsageLimit.HasValue && v.UsedCount >= v.UsageLimit ? "exhausted"
                    : "active"
            });

            return Ok(result);
        }

        // POST /api/seller/vouchers
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateVoucherDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code))
                return BadRequest(new { message = "Mã voucher là bắt buộc" });
            if (dto.DiscountValue <= 0)
                return BadRequest(new { message = "Giá trị giảm phải lớn hơn 0" });
            if (dto.DiscountType == "percent" && dto.DiscountValue > 100)
                return BadRequest(new { message = "Giảm theo % không được vượt quá 100" });
            if (dto.EndDate.HasValue && dto.StartDate.HasValue && dto.EndDate <= dto.StartDate)
                return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu" });

            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            // Code unique within this shop
            var exists = await _db.Vouchers.AnyAsync(v => v.ShopId == shop!.Id && v.Code == dto.Code.ToUpper());
            if (exists) return Conflict(new { message = "Mã voucher đã tồn tại trong shop" });

            var voucher = new Voucher
            {
                Code           = dto.Code.ToUpper().Trim(),
                DiscountType   = dto.DiscountType ?? "percent",
                DiscountValue  = dto.DiscountValue,
                MinOrderAmount = dto.MinOrderValue ?? 0,
                UsageLimit     = dto.MaxUsage,
                StartDate      = dto.StartDate,
                ExpiryDate     = dto.EndDate,
                IsActive       = true,
                ShopId         = shop!.Id
            };

            _db.Vouchers.Add(voucher);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Voucher đã được tạo", id = voucher.Id });
        }

        // PUT /api/seller/vouchers/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateVoucherDto dto)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Id == id && v.ShopId == shop!.Id);
            if (voucher == null) return NotFound(new { message = "Voucher không tồn tại" });
            if (voucher.UsedCount > 0)
                return BadRequest(new { message = "Không thể sửa voucher đã có người dùng" });

            if (dto.DiscountValue.HasValue && dto.DiscountValue <= 0)
                return BadRequest(new { message = "Giá trị giảm phải lớn hơn 0" });
            if (dto.DiscountType == "percent" && (dto.DiscountValue ?? voucher.DiscountValue) > 100)
                return BadRequest(new { message = "Giảm theo % không được vượt quá 100" });

            voucher.DiscountType   = dto.DiscountType ?? voucher.DiscountType;
            voucher.DiscountValue  = dto.DiscountValue ?? voucher.DiscountValue;
            voucher.MinOrderAmount = dto.MinOrderValue ?? voucher.MinOrderAmount;
            voucher.UsageLimit     = dto.MaxUsage ?? voucher.UsageLimit;
            voucher.StartDate      = dto.StartDate ?? voucher.StartDate;
            voucher.ExpiryDate     = dto.EndDate ?? voucher.ExpiryDate;
            voucher.IsActive       = dto.IsActive ?? voucher.IsActive;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Voucher đã được cập nhật" });
        }

        // DELETE /api/seller/vouchers/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Id == id && v.ShopId == shop!.Id);
            if (voucher == null) return NotFound(new { message = "Voucher không tồn tại" });

            if (voucher.UsedCount > 0)
            {
                // Soft delete
                voucher.IsActive = false;
                await _db.SaveChangesAsync();
                return Ok(new { message = "Voucher đã được vô hiệu hóa" });
            }

            // Hard delete
            _db.Vouchers.Remove(voucher);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Voucher đã bị xóa" });
        }
    }

    public class CreateVoucherDto
    {
        public string Code { get; set; } = "";
        public string? DiscountType { get; set; }
        public decimal DiscountValue { get; set; }
        public decimal? MinOrderValue { get; set; }
        public int? MaxUsage { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class UpdateVoucherDto
    {
        public string? DiscountType { get; set; }
        public decimal? DiscountValue { get; set; }
        public decimal? MinOrderValue { get; set; }
        public int? MaxUsage { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool? IsActive { get; set; }
    }
}
