using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Repository.EFCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartRepositoryEF _cartRepo;
        private readonly IProductRepositoryEF _productRepo;
        private readonly MySqlDbContext _context;

        public CartController(ICartRepositoryEF cartRepo, IProductRepositoryEF productRepo, MySqlDbContext context)
        {
            _cartRepo = cartRepo;
            _productRepo = productRepo;
            _context = context;
        }

        private string? GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // ========== USER ENDPOINTS (dành cho khách hàng đang đăng nhập) ==========

        /// <summary>Xem giỏ hàng của chính mình</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyCart()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var items = await _cartRepo.GetByUserAsync(userId);
            var total = items.Sum(i => i.Product!.Price * i.Quantity);

            return Ok(new
            {
                items = items.Select(i => new
                {
                    i.Id,
                    i.ProductId,
                    productName = i.Product!.Name,
                    productImage = i.Product.ImageUrl,
                    unitPrice = i.Product.Price,
                    i.Quantity,
                    subTotal = i.Product.Price * i.Quantity,
                    stockAvailable = i.Product.Stock
                }),
                totalAmount = total,
                totalItems = items.Sum(i => i.Quantity)
            });
        }

        /// <summary>Thêm sản phẩm vào giỏ (hoặc tăng số lượng)</summary>
        [HttpPost("add")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var product = await _productRepo.GetByIdAsync(dto.ProductId);
            if (product == null || !product.IsActive)
                return BadRequest(new { message = "Sản phẩm không tồn tại." });

            if (product.Stock < dto.Quantity)
                return BadRequest(new { message = $"Chỉ còn {product.Stock} sản phẩm trong kho." });

            var existing = await _cartRepo.GetByUserAndProductAsync(userId, dto.ProductId);
            if (existing != null)
            {
                var newQty = existing.Quantity + dto.Quantity;
                if (newQty > product.Stock)
                    return BadRequest(new { message = $"Tổng số lượng vượt tồn kho ({product.Stock})." });
                existing.Quantity = newQty;
                await _cartRepo.UpdateAsync(existing);
            }
            else
            {
                await _cartRepo.AddAsync(new CartItem
                {
                    UserId = userId,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    AddedAt = DateTime.UtcNow
                });
            }

            return Ok(new { message = "Đã thêm vào giỏ hàng." });
        }

        /// <summary>Cập nhật số lượng của một sản phẩm trong giỏ</summary>
        //[HttpPut("update")]
        //public async Task<IActionResult> UpdateCart([FromBody] UpdateCartDto dto)
        //{
        //    var userId = GetUserId();
        //    if (string.IsNullOrEmpty(userId)) return Unauthorized();

        //    var item = await _cartRepo.GetByIdAsync(dto.CartItemId);
        //    if (item == null || item.UserId != userId)
        //        return NotFound(new { message = "Sản phẩm không có trong giỏ." });

        //    if (dto.Quantity <= 0)
        //        await _cartRepo.DeleteAsync(item);
        //    else
        //    {
        //        var product = await _productRepo.GetByIdAsync(item.ProductId);
        //        if (product != null && dto.Quantity > product.Stock)
        //            return BadRequest(new { message = $"Chỉ còn {product.Stock} sản phẩm trong kho." });
        //        item.Quantity = dto.Quantity;
        //        await _cartRepo.UpdateAsync(item);
        //    }

        //    return Ok(new { message = "Đã cập nhật giỏ hàng." });
        //}

        /// <summary>Xóa một sản phẩm khỏi giỏ</summary>
        [HttpDelete("{cartItemId}")]
        public async Task<IActionResult> RemoveFromCart(int cartItemId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var item = await _cartRepo.GetByIdAsync(cartItemId);
            if (item == null || item.UserId != userId)
                return NotFound(new { message = "Sản phẩm không có trong giỏ." });

            await _cartRepo.DeleteAsync(item);
            return Ok(new { message = "Đã xóa sản phẩm khỏi giỏ hàng." });
        }

        /// <summary>Xóa toàn bộ giỏ hàng của chính mình</summary>
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearMyCart()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var items = await _cartRepo.GetByUserAsync(userId);
            foreach (var item in items)
                await _cartRepo.DeleteAsync(item);

            return Ok(new { message = "Đã xóa toàn bộ giỏ hàng." });
        }

        // ========== ADMIN ENDPOINTS (yêu cầu quyền Admin) ==========

        // GET: api/cart/all – lấy tất cả giỏ hàng (admin)
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllCarts()
        {
            var users = await _context.Users.Where(u => u.UserType == 0).ToListAsync(); // chỉ khách hàng
            var result = new List<object>();

            foreach (var user in users)
            {
                var items = await _cartRepo.GetByUserAsync(user.Id);
                if (items.Any())
                {
                    var total = items.Sum(i => i.Product!.Price * i.Quantity);
                    result.Add(new
                    {
                        userId = user.Id,
                        userName = user.Name ?? user.UserName,
                        items = items.Select(i => new
                        {
                            i.Id,
                            i.ProductId,
                            productName = i.Product!.Name,
                            productImage = i.Product.ImageUrl,
                            unitPrice = i.Product.Price,
                            i.Quantity,
                            subTotal = i.Product.Price * i.Quantity
                        }),
                        totalAmount = total,
                        totalItems = items.Sum(i => i.Quantity)
                    });
                }
            }
            return Ok(result);
        }

        // PUT: api/cart/update – cập nhật số lượng item (admin)
        [HttpPut("update")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCartForAdmin([FromBody] UpdateCartDto dto)
        {
            var item = await _cartRepo.GetByIdAsync(dto.CartItemId);
            if (item == null) return NotFound();

            if (dto.Quantity <= 0)
                await _cartRepo.DeleteAsync(item);
            else
            {
                var product = await _productRepo.GetByIdAsync(item.ProductId);
                if (product != null && dto.Quantity > product.Stock)
                    return BadRequest(new { message = "Số lượng vượt tồn kho." });
                item.Quantity = dto.Quantity;
                await _cartRepo.UpdateAsync(item);
            }
            return Ok(new { message = "Đã cập nhật." });
        }

        // DELETE: api/cart/{cartItemId} – xóa một item (admin)
        [HttpDelete("{cartItemId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RemoveCartItem(int cartItemId)
        {
            var item = await _cartRepo.GetByIdAsync(cartItemId);
            if (item == null) return NotFound();
            await _cartRepo.DeleteAsync(item);
            return Ok(new { message = "Đã xóa." });
        }

        // DELETE: api/cart/clear/{userId} – xóa toàn bộ giỏ của user (admin)
        [HttpDelete("clear/{userId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ClearUserCart(string userId)
        {
            var items = await _cartRepo.GetByUserAsync(userId);
            foreach (var item in items)
                await _cartRepo.DeleteAsync(item);
            return Ok(new { message = "Đã xóa toàn bộ giỏ." });
        }

    }
        // DTOs
        public class AddToCartDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class UpdateCartDto
    {
        public int CartItemId { get; set; }
        public int Quantity { get; set; }
    }
}