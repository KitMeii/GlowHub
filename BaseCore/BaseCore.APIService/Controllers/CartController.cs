using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BaseCore.Entities;
using BaseCore.Repository.EFCore;
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

        public CartController(ICartRepositoryEF cartRepo, IProductRepositoryEF productRepo)
        {
            _cartRepo = cartRepo;
            _productRepo = productRepo;
        }

        private string? GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>Xem giỏ hàng hiện tại</summary>
        [HttpGet]
        public async Task<IActionResult> GetCart()
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

        /// <summary>Thêm sản phẩm vào giỏ</summary>
        [HttpPost("add")]
        public async Task<IActionResult> Add([FromBody] AddToCartDto dto)
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
                    return BadRequest(new { message = $"Tổng vượt tồn kho ({product.Stock})." });
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

            return Ok(new { message = "Đã thêm vào giỏ hàng!" });
        }

        /// <summary>Cập nhật số lượng</summary>
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateCartDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var item = await _cartRepo.GetByIdAsync(dto.CartItemId);
            if (item == null || ((CartItem)item).UserId != userId)
                return NotFound(new { message = "Không tìm thấy sản phẩm trong giỏ." });

            var cartItem = (CartItem)item;
            if (dto.Quantity <= 0)
                await _cartRepo.DeleteAsync(cartItem);
            else
            {
                cartItem.Quantity = dto.Quantity;
                await _cartRepo.UpdateAsync(cartItem);
            }

            return Ok(new { message = "Đã cập nhật giỏ hàng." });
        }

        /// <summary>Xóa 1 sản phẩm khỏi giỏ</summary>
        [HttpDelete("{cartItemId}")]
        public async Task<IActionResult> Remove(int cartItemId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var item = await _cartRepo.GetByIdAsync(cartItemId) as CartItem;
            if (item == null || item.UserId != userId)
                return NotFound(new { message = "Không tìm thấy mục trong giỏ." });

            await _cartRepo.DeleteAsync(item);
            return Ok(new { message = "Đã xóa khỏi giỏ hàng." });
        }

        /// <summary>Xóa toàn bộ giỏ</summary>
        [HttpDelete("clear")]
        public async Task<IActionResult> Clear()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            await _cartRepo.ClearByUserAsync(userId);
            return Ok(new { message = "Đã xóa giỏ hàng." });
        }
    }

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