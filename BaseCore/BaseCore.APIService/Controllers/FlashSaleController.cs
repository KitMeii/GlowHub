using BaseCore.Entities;
using BaseCore.Repository;
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

        public FlashSaleController(MySqlDbContext db) => _db = db;

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

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
                        FlashSaleId = sale.Id,
                        ProductId = p.ProductId,
                        SalePrice = p.SalePrice,
                        OriginalPrice = product.Price,
                        Quantity = p.Quantity,
                        SoldCount = 0,
                        IsActive = true
                    });
                }
                await _db.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(GetById), new { id = sale.Id }, new { id = sale.Id });
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
}
