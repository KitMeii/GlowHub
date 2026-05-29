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
    public class FlashSaleController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;

        public FlashSaleController(MySqlDbContext db, AuditLogService audit)
        {
            _db    = db;
            _audit = audit;
        }

        private string? GetUserId()   => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

        /// <summary>GET /api/flashsale/active — Flash sale đang diễn ra</summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var now = DateTime.UtcNow;
            var sale = await _db.FlashSales
                .Where(fs => fs.IsActive && fs.StartTime <= now && fs.EndTime > now)
                .OrderByDescending(fs => fs.StartTime)
                .FirstOrDefaultAsync();

            if (sale == null) return Ok(null);

            var products = await _db.FlashSaleProducts
                .Include(p => p.Product)
                .Where(p => p.FlashSaleId == sale.Id && p.IsActive)
                .ToListAsync();

            return Ok(new
            {
                id = sale.Id,
                name = sale.Name,
                startTime = sale.StartTime,
                endTime = sale.EndTime,
                secondsRemaining = (int)(sale.EndTime - now).TotalSeconds,
                products = products.Select(p => new
                {
                    productId = p.ProductId,
                    name = p.Product.Name,
                    image = p.Product.ImageUrl,
                    originalPrice = p.OriginalPrice,
                    salePrice = p.SalePrice,
                    discountPercent = p.OriginalPrice > 0
                        ? (int)Math.Round((p.OriginalPrice - p.SalePrice) / p.OriginalPrice * 100)
                        : 0,
                    soldCount = p.SoldCount,
                    totalQuantity = p.Quantity,
                    remainingQuantity = p.Quantity - p.SoldCount
                })
            });
        }

        /// <summary>GET /api/flashsale/upcoming — Flash sale sắp diễn ra (24h tới)</summary>
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming()
        {
            var now = DateTime.UtcNow;
            var in24h = now.AddHours(24);

            var sales = await _db.FlashSales
                .Where(fs => fs.IsActive && fs.StartTime > now && fs.StartTime <= in24h)
                .OrderBy(fs => fs.StartTime)
                .ToListAsync();

            var result = new List<object>();
            foreach (var sale in sales)
            {
                var products = await _db.FlashSaleProducts
                    .Include(p => p.Product)
                    .Where(p => p.FlashSaleId == sale.Id && p.IsActive)
                    .ToListAsync();

                result.Add(new
                {
                    id = sale.Id,
                    name = sale.Name,
                    startTime = sale.StartTime,
                    endTime = sale.EndTime,
                    secondsUntilStart = (int)(sale.StartTime - now).TotalSeconds,
                    products = products.Select(p => new
                    {
                        productId = p.ProductId,
                        name = p.Product.Name,
                        image = p.Product.ImageUrl,
                        originalPrice = p.OriginalPrice,
                        salePrice = p.SalePrice,
                        discountPercent = p.OriginalPrice > 0
                            ? (int)Math.Round((p.OriginalPrice - p.SalePrice) / p.OriginalPrice * 100)
                            : 0,
                        soldCount = p.SoldCount,
                        totalQuantity = p.Quantity,
                        remainingQuantity = p.Quantity - p.SoldCount
                    })
                });
            }

            return Ok(result);
        }

        /// <summary>GET /api/flashsale/{id} — Chi tiết 1 flash sale</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var now = DateTime.UtcNow;
            var sale = await _db.FlashSales.FindAsync(id);
            if (sale == null) return NotFound();

            var products = await _db.FlashSaleProducts
                .Include(p => p.Product)
                .Where(p => p.FlashSaleId == id && p.IsActive)
                .ToListAsync();

            return Ok(new
            {
                id = sale.Id,
                name = sale.Name,
                startTime = sale.StartTime,
                endTime = sale.EndTime,
                isActive = sale.IsActive,
                secondsRemaining = sale.EndTime > now ? (int)(sale.EndTime - now).TotalSeconds : 0,
                products = products.Select(p => new
                {
                    productId = p.ProductId,
                    name = p.Product.Name,
                    image = p.Product.ImageUrl,
                    originalPrice = p.OriginalPrice,
                    salePrice = p.SalePrice,
                    discountPercent = p.OriginalPrice > 0
                        ? (int)Math.Round((p.OriginalPrice - p.SalePrice) / p.OriginalPrice * 100)
                        : 0,
                    soldCount = p.SoldCount,
                    totalQuantity = p.Quantity,
                    remainingQuantity = p.Quantity - p.SoldCount
                })
            });
        }

        /// <summary>POST /api/flashsale — Tạo flash sale (Admin)</summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateFlashSaleDto dto)
        {
            var sale = new FlashSale
            {
                Name = dto.Name,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = GetUserId()
            };
            _db.FlashSales.Add(sale);
            await _db.SaveChangesAsync();

            if (dto.Products != null)
            {
                foreach (var p in dto.Products)
                {
                    var product = await _db.Products.FindAsync(p.ProductId);
                    if (product == null) continue;

                    _db.FlashSaleProducts.Add(new FlashSaleProduct
                    {
                        FlashSaleId   = sale.Id,
                        ProductId     = p.ProductId,
                        SalePrice     = p.SalePrice,
                        OriginalPrice = product.Price,
                        Quantity      = p.Quantity,
                        SoldCount     = 0,
                        IsActive      = true
                    });
                }
                await _db.SaveChangesAsync();
            }

            await _audit.Log(GetUserId(), GetUserName(), "FLASHSALE_CREATE", "FlashSale", sale.Id.ToString(),
                null, new { name = sale.Name, startTime = sale.StartTime, endTime = sale.EndTime });

            return CreatedAtAction(nameof(GetById), new { id = sale.Id }, new { id = sale.Id });
        }

        /// <summary>GET /api/admin/flashsales — Danh sách tất cả flash sale (Admin)</summary>
        [HttpGet("admin/list")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminList([FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            var now = DateTime.UtcNow;
            var total = await _db.FlashSales.CountAsync();
            var sales = await _db.FlashSales
                .OrderByDescending(fs => fs.StartTime)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var result = new List<object>();
            foreach (var fs in sales)
            {
                var productCount = await _db.FlashSaleProducts.CountAsync(p => p.FlashSaleId == fs.Id);
                var soldCount    = await _db.FlashSaleProducts.Where(p => p.FlashSaleId == fs.Id).SumAsync(p => p.SoldCount);
                string saleState = fs.StartTime > now ? "upcoming" : (fs.EndTime > now && fs.IsActive ? "active" : "ended");

                result.Add(new {
                    fs.Id, fs.Name, fs.StartTime, fs.EndTime, fs.IsActive,
                    productCount, soldCount, state = saleState
                });
            }

            return Ok(new { items = result, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        /// <summary>PUT /api/flashsale/{id} — Cập nhật flash sale (Admin)</summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateFlashSaleDto dto)
        {
            var sale = await _db.FlashSales.FindAsync(id);
            if (sale == null) return NotFound(new { message = "Không tìm thấy flash sale" });

            if (!string.IsNullOrWhiteSpace(dto.Name)) sale.Name = dto.Name;
            if (dto.StartTime.HasValue) sale.StartTime = dto.StartTime.Value;
            if (dto.EndTime.HasValue) sale.EndTime = dto.EndTime.Value;
            if (dto.IsActive.HasValue) sale.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync();
            await _audit.Log(GetUserId(), GetUserName(), "FLASHSALE_UPDATE", "FlashSale", id.ToString(),
                null, new { name = sale.Name, isActive = sale.IsActive });
            return Ok(new { message = "Đã cập nhật flash sale" });
        }

        /// <summary>DELETE /api/flashsale/{id} — Xóa flash sale (Admin)</summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var sale = await _db.FlashSales.FindAsync(id);
            if (sale == null) return NotFound(new { message = "Không tìm thấy flash sale" });

            await _audit.Log(GetUserId(), GetUserName(), "FLASHSALE_DELETE", "FlashSale", id.ToString(),
                new { name = sale.Name }, null);
            _db.FlashSales.Remove(sale);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa flash sale" });
        }

        /// <summary>PUT /api/flashsale/{id}/toggle — Bật/tắt flash sale (Admin)</summary>
        [HttpPut("{id:int}/toggle")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Toggle(int id)
        {
            var sale = await _db.FlashSales.FindAsync(id);
            if (sale == null) return NotFound(new { message = "Không tìm thấy flash sale" });

            sale.IsActive = !sale.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new { message = sale.IsActive ? "Đã bật flash sale" : "Đã tắt flash sale", isActive = sale.IsActive });
        }
    }

    public class CreateFlashSaleDto
    {
        public string Name { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public List<FlashSaleProductDto>? Products { get; set; }
    }

    public class FlashSaleProductDto
    {
        public int ProductId { get; set; }
        public decimal SalePrice { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateFlashSaleDto
    {
        public string? Name { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool? IsActive { get; set; }
    }
}
