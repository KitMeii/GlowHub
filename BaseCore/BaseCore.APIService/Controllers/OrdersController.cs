using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BaseCore.Entities;
using BaseCore.Repository.EFCore;
using BaseCore.Repository;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderRepositoryEF _orderRepository;
        private readonly IOrderDetailRepositoryEF _orderDetailRepository;
        private readonly IProductRepositoryEF _productRepository;
        private readonly ICartRepositoryEF _cartRepository;
        private readonly MySqlDbContext _db;

        public OrdersController(
            IOrderRepositoryEF orderRepository,
            IOrderDetailRepositoryEF orderDetailRepository,
            IProductRepositoryEF productRepository,
            ICartRepositoryEF cartRepository,
            MySqlDbContext db)
        {
            _orderRepository = orderRepository;
            _orderDetailRepository = orderDetailRepository;
            _productRepository = productRepository;
            _cartRepository = cartRepository;
            _db = db;
        }

        private string? GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>Lấy đơn hàng của user đang đăng nhập</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var orders = await _orderRepository.GetByUserAsync(userId);
            return Ok(orders);
        }

        /// <summary>Chi tiết 1 đơn hàng kèm sản phẩm</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _orderRepository.GetWithDetailsAsync(id);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            return Ok(order);
        }

        /// <summary>Tất cả đơn hàng — chỉ Admin</summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders([FromQuery] string? status = null)
        {
            var orders = await _orderRepository.GetAllWithDetailsAsync(status);
            return Ok(orders);
        }

        /// <summary>
        /// ★ CHECKOUT — Sacred Checkout với Transaction ACID
        /// Toàn bộ: kiểm kho → trừ kho (RowVersion) → tạo đơn → xóa giỏ
        /// gói trong 1 Transaction. Lỗi bất kỳ bước → Rollback toàn bộ.
        /// </summary>
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.ShippingAddress))
                return BadRequest(new { message = "Vui lòng nhập địa chỉ giao hàng." });

            // Lấy giỏ hàng từ DB
            var cartItems = await _cartRepository.GetByUserAsync(userId);
            if (!cartItems.Any())
                return BadRequest(new { message = "Giỏ hàng trống." });

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                decimal totalAmount = 0;
                var orderDetails = new List<OrderDetail>();

                foreach (var cartItem in cartItems)
                {
                    // Đọc tồn kho thực tế từ DB (không tin cache)
                    var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == cartItem.ProductId);

                    if (product == null || !product.IsActive)
                        throw new Exception($"Sản phẩm '{cartItem.Product?.Name}' không còn bán.");

                    if (product.Stock < cartItem.Quantity)
                        throw new Exception($"'{product.Name}' chỉ còn {product.Stock}, bạn đặt {cartItem.Quantity}.");

                    // Backend tự tính giá — không tin Frontend
                    decimal lockedPrice = product.Price;
                    totalAmount += lockedPrice * cartItem.Quantity;

                    // Trừ kho — EF tự thêm WHERE RowVersion = @original
                    // → Nếu user khác vừa mua: DbUpdateConcurrencyException
                    product.Stock -= cartItem.Quantity;

                    orderDetails.Add(new OrderDetail
                    {
                        ProductId = product.Id,
                        Quantity = cartItem.Quantity,
                        UnitPrice = lockedPrice   // ★ Giá chốt cứng
                    });
                }

                // Tạo Order
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    Status = OrderStatus.Pending,
                    ShippingAddress = dto.ShippingAddress,
                    Note = dto.Note,
                    OrderDetails = orderDetails
                };
                _db.Orders.Add(order);

                // Xóa giỏ hàng
                _db.CartItems.RemoveRange(cartItems);

                await _db.SaveChangesAsync();  // ← Nếu có Concurrency → exception → catch
                await transaction.CommitAsync();

                return Ok(new { message = "Đặt hàng thành công!", orderId = order.Id, totalAmount });
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "Có sản phẩm vừa được mua hết. Vui lòng kiểm tra lại giỏ hàng." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Admin cập nhật trạng thái — hủy đơn tự động Restock</summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            var validStatuses = new[] {
                OrderStatus.Pending, OrderStatus.Confirmed,
                OrderStatus.Shipping, OrderStatus.Completed, OrderStatus.Cancelled
            };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest(new { message = "Trạng thái không hợp lệ." });

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });

                var oldStatus = order.Status;
                order.Status = dto.Status;
                order.UpdatedAt = DateTime.UtcNow;

                // ★ RESTOCK: hủy đơn → trả hàng về kho
                if (dto.Status == OrderStatus.Cancelled
                    && oldStatus != OrderStatus.Cancelled
                    && oldStatus != OrderStatus.Completed)
                {
                    foreach (var detail in order.OrderDetails)
                    {
                        var product = await _db.Products.FindAsync(detail.ProductId);
                        if (product != null) product.Stock += detail.Quantity;
                    }
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = $"Đã cập nhật đơn #{id} → {dto.Status}", order });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class CheckoutDto
    {
        public string ShippingAddress { get; set; } = "";
        public string? Note { get; set; }
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } = "";
    }
}