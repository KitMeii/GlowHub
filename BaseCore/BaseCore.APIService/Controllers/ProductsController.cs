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
    public class ProductsController : ControllerBase
    {
        private readonly IProductRepositoryEF _productRepository;
        private readonly ICategoryRepositoryEF _categoryRepository;
        private readonly IShopRepositoryEF _shopRepository;
        private readonly MySqlDbContext _db;

        public ProductsController(
            IProductRepositoryEF productRepository,
            ICategoryRepositoryEF categoryRepository,
            IShopRepositoryEF shopRepository,
            MySqlDbContext db)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _shopRepository = shopRepository;
            _db = db;
        }

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<(Shop? shop, IActionResult? error)> GetActiveShopAsync()
        {
            var sellerId = GetUserId();
            var shop = await _shopRepository.GetBySellerIdAsync(sellerId!);
            if (shop == null)
                return (null, NotFound(new { message = "Bạn chưa có shop" }));
            if (shop.Status != ShopStatus.Active)
                return (null, BadRequest(new { message = "Shop chưa được duyệt hoặc đã bị khóa" }));
            return (shop, null);
        }

        // ─────────────────────────────────────────────────────────
        // PUBLIC
        // ─────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? keyword,
            [FromQuery] int? categoryId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var (products, totalCount) = await _productRepository.SearchAsync(keyword, categoryId, page, pageSize);
            return Ok(new
            {
                items = products,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { message = "Product not found" });
            return Ok(product);
        }

        [HttpGet("category/{categoryId}")]
        public async Task<IActionResult> GetByCategory(int categoryId)
        {
            var products = await _productRepository.GetByCategoryAsync(categoryId);
            return Ok(products);
        }

        // ─────────────────────────────────────────────────────────
        // SELLER — my products
        // ─────────────────────────────────────────────────────────

        // GET /api/products/my?page=1&limit=10&search=&status=all
        [HttpGet("my")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetMyProducts(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = "all")
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var query = _db.Products
                .Include(p => p.Category)
                .Where(p => p.ShopId == shop!.Id)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Name.Contains(search));

            if (status == "active")
                query = query.Where(p => p.IsActive);
            else if (status == "inactive")
                query = query.Where(p => !p.IsActive);

            var total = await query.CountAsync();
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            // soldCount per product
            var ids = products.Select(p => p.Id).ToList();
            var soldMap = await _db.OrderDetails
                .Where(od => ids.Contains(od.ProductId))
                .GroupBy(od => od.ProductId)
                .Select(g => new { g.Key, count = g.Sum(od => od.Quantity) })
                .ToDictionaryAsync(x => x.Key, x => x.count);

            var items = products.Select(p => new
            {
                p.Id, p.Name, p.Price, p.DiscountPrice,
                p.Stock, p.IsActive, p.ImageUrl,
                category = p.Category?.Name,
                soldCount = soldMap.GetValueOrDefault(p.Id, 0)
            });

            return Ok(new { items, total, page, limit, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        // GET /api/products/{id}/stats
        [HttpGet("{id:int}/stats")]
        [Authorize(Roles = "Admin,Seller")]
        public async Task<IActionResult> GetProductStats(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { message = "Product not found" });

            // Ownership check for Seller
            if (User.IsInRole(RoleConstant.Seller) && !User.IsInRole(RoleConstant.Admin))
            {
                var (shop, err) = await GetActiveShopAsync();
                if (err != null) return err;
                if (product.ShopId != shop!.Id) return Forbid();
            }

            var soldCount = await _db.OrderDetails
                .Where(od => od.ProductId == id)
                .SumAsync(od => (int?)od.Quantity) ?? 0;

            var reviews = await _db.Reviews.Where(r => r.ProductId == id).ToListAsync();
            var reviewCount = reviews.Count;
            var avgRating = reviewCount > 0 ? reviews.Average(r => r.Rating) : 0.0;

            return Ok(new { soldCount, viewCount = 0, reviewCount, avgRating });
        }

        // ─────────────────────────────────────────────────────────
        // ADMIN / SELLER — CRUD
        // ─────────────────────────────────────────────────────────

        // POST /api/products
        [HttpPost]
        [Authorize(Roles = "Admin,Seller")]
        public async Task<IActionResult> Create([FromBody] ProductCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Tên sản phẩm là bắt buộc" });
            if (dto.Price <= 0)
                return BadRequest(new { message = "Giá phải lớn hơn 0" });
            if (dto.Stock < 0)
                return BadRequest(new { message = "Tồn kho không được âm" });
            if (dto.DiscountPrice.HasValue && dto.DiscountPrice >= dto.Price)
                return BadRequest(new { message = "Giá khuyến mãi phải nhỏ hơn giá gốc" });
            if (dto.CategoryId <= 0)
                return BadRequest(new { message = "Danh mục là bắt buộc" });

            var category = await _categoryRepository.GetByIdAsync(dto.CategoryId);
            if (category == null) return BadRequest(new { message = "Danh mục không tồn tại" });

            string? shopId = null;
            if (User.IsInRole(RoleConstant.Seller) && !User.IsInRole(RoleConstant.Admin))
            {
                var (shop, err) = await GetActiveShopAsync();
                if (err != null) return err;
                shopId = shop!.Id;
            }

            var product = new Product
            {
                Name          = dto.Name,
                Price         = dto.Price,
                Stock         = dto.Stock,
                CategoryId    = dto.CategoryId,
                Description   = dto.Description ?? "",
                ImageUrl      = dto.ImageUrl ?? "",
                DiscountPrice = dto.DiscountPrice,
                IsActive      = dto.IsActive ?? true,
                ShopId        = shopId
            };

            await _productRepository.AddAsync(product);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        // PUT /api/products/{id}
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Seller")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductUpdateDto dto)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { message = "Product not found" });

            // Seller: verify ownership
            if (User.IsInRole(RoleConstant.Seller) && !User.IsInRole(RoleConstant.Admin))
            {
                var (shop, err) = await GetActiveShopAsync();
                if (err != null) return err;
                if (product.ShopId != shop!.Id) return Forbid();
            }

            if (dto.DiscountPrice.HasValue)
            {
                var effectivePrice = dto.Price ?? product.Price;
                if (dto.DiscountPrice >= effectivePrice)
                    return BadRequest(new { message = "Giá khuyến mãi phải nhỏ hơn giá gốc" });
            }

            product.Name          = dto.Name ?? product.Name;
            product.Price         = dto.Price ?? product.Price;
            product.Stock         = dto.Stock ?? product.Stock;
            product.CategoryId    = dto.CategoryId ?? product.CategoryId;
            product.Description   = dto.Description ?? product.Description;
            product.ImageUrl      = dto.ImageUrl ?? product.ImageUrl;
            product.DiscountPrice = dto.DiscountPrice ?? product.DiscountPrice;
            product.IsActive      = dto.IsActive ?? product.IsActive;

            await _productRepository.UpdateAsync(product);
            return Ok(product);
        }

        // DELETE /api/products/{id} — soft delete for Seller
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin,Seller")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { message = "Product not found" });

            if (User.IsInRole(RoleConstant.Seller) && !User.IsInRole(RoleConstant.Admin))
            {
                var (shop, err) = await GetActiveShopAsync();
                if (err != null) return err;
                if (product.ShopId != shop!.Id) return Forbid();

                // Soft delete for Seller
                product.IsActive = false;
                await _productRepository.UpdateAsync(product);
                return Ok(new { message = "Sản phẩm đã được ẩn" });
            }

            // Hard delete for Admin
            await _productRepository.DeleteAsync(product);
            return Ok(new { message = "Sản phẩm đã bị xóa" });
        }

        // PATCH /api/products/{id}/toggle
        [HttpPatch("{id:int}/toggle")]
        [Authorize(Roles = "Admin,Seller")]
        public async Task<IActionResult> Toggle(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound(new { message = "Product not found" });

            if (User.IsInRole(RoleConstant.Seller) && !User.IsInRole(RoleConstant.Admin))
            {
                var (shop, err) = await GetActiveShopAsync();
                if (err != null) return err;
                if (product.ShopId != shop!.Id) return Forbid();
            }

            product.IsActive = !product.IsActive;
            await _productRepository.UpdateAsync(product);
            return Ok(new { isActive = product.IsActive });
        }
    }

    // DTOs
    public class ProductCreateDto
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int CategoryId { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? DiscountPrice { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ProductUpdateDto
    {
        public string? Name { get; set; }
        public decimal? Price { get; set; }
        public int? Stock { get; set; }
        public int? CategoryId { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? DiscountPrice { get; set; }
        public bool? IsActive { get; set; }
    }
}
