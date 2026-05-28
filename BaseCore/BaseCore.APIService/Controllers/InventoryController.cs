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
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = RoleConstant.Seller)]
    public class InventoryController : ControllerBase
    {
        private readonly IShopRepositoryEF _shopRepository;
        private readonly MySqlDbContext _db;

        public InventoryController(IShopRepositoryEF shopRepository, MySqlDbContext db)
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

        // GET /api/inventory/my
        [HttpGet("my")]
        public async Task<IActionResult> GetAll()
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var products = await _db.Products
                .Include(p => p.Category)
                .Where(p => p.ShopId == shop!.Id)
                .OrderBy(p => p.Stock)
                .Select(p => new
                {
                    p.Id, p.Name, p.Stock, p.IsActive,
                    p.ImageUrl,
                    category  = p.Category != null ? p.Category.Name : "",
                    lowStock  = p.Stock < 10
                })
                .ToListAsync();

            return Ok(products);
        }

        // GET /api/inventory/low-stock
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStock()
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var products = await _db.Products
                .Where(p => p.ShopId == shop!.Id && p.Stock < 10 && p.IsActive)
                .OrderBy(p => p.Stock)
                .Select(p => new { p.Id, p.Name, p.Stock, p.ImageUrl })
                .ToListAsync();

            return Ok(products);
        }

        // PUT /api/inventory/{productId}
        [HttpPut("{productId:int}")]
        public async Task<IActionResult> UpdateStock(int productId, [FromBody] UpdateStockDto dto)
        {
            if (dto.Stock < 0)
                return BadRequest(new { message = "Tồn kho không được âm" });

            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.ShopId == shop!.Id);
            if (product == null) return NotFound(new { message = "Sản phẩm không tồn tại hoặc không thuộc shop của bạn" });

            product.Stock = dto.Stock;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Tồn kho đã được cập nhật", stock = product.Stock });
        }
    }

    public class UpdateStockDto
    {
        public int Stock { get; set; }
    }
}
