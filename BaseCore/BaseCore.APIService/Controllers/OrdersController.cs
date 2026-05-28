using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BaseCore.Common;
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
        private readonly IShopRepositoryEF _shopRepository;
        private readonly MySqlDbContext _db;

        public OrdersController(
            IOrderRepositoryEF orderRepository,
            IOrderDetailRepositoryEF orderDetailRepository,
            IProductRepositoryEF productRepository,
            ICartRepositoryEF cartRepository,
            IShopRepositoryEF shopRepository,
            MySqlDbContext db)
        {
            _orderRepository = orderRepository;
            _orderDetailRepository = orderDetailRepository;
            _productRepository = productRepository;
            _cartRepository = cartRepository;
            _shopRepository = shopRepository;
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

        /// <summary>Tất cả đơn hàng — Admin và Seller</summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin,Seller")]
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
        /// <summary>User tự hủy đơn của mình (chỉ khi Pending)</summary>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelMyOrder(int id)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

                if (order == null)
                    return NotFound(new { message = "Không tìm thấy đơn hàng." });

                if (order.Status != OrderStatus.Pending)
                    return BadRequest(new { message = "Chỉ có thể hủy đơn đang chờ xác nhận." });

                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                // Hoàn lại tồn kho
                foreach (var detail in order.OrderDetails)
                {
                    var product = await _db.Products.FindAsync(detail.ProductId);
                    if (product != null) product.Stock += detail.Quantity;
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = $"Đã hủy đơn hàng #{id}." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // SELLER — shop order endpoints
        // ─────────────────────────────────────────────────────────

        private async Task<(Shop? shop, IActionResult? error)> GetActiveShopAsync()
        {
            var sellerId = GetUserId();
            var shop = await _shopRepository.GetBySellerIdAsync(sellerId!);
            if (shop == null) return (null, NotFound(new { message = "Bạn chưa có shop" }));
            if (shop.Status != ShopStatus.Active) return (null, BadRequest(new { message = "Shop chưa được duyệt hoặc đã bị khóa" }));
            return (shop, null);
        }

        // GET /api/orders/shop?status=&page=1&limit=10
        [HttpGet("shop")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetShopOrders(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await _db.Products
                .Where(p => p.ShopId == shop!.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var query = _db.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.User)
                .Where(o => o.OrderDetails.Any(od => productIds.Contains(od.ProductId)))
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status.ToUpper());

            var total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            var items = orders.Select(o => new
            {
                orderId         = o.Id,
                customerName    = o.User?.Name ?? "",
                customerPhone   = o.User?.Phone ?? "",
                shippingAddress = o.ShippingAddress,
                status          = o.Status,
                totalAmount     = o.TotalAmount,
                createdAt       = o.OrderDate,
                cancelReason    = o.CancelReason,
                trackingCode    = o.TrackingCode,
                items           = o.OrderDetails
                    .Where(od => productIds.Contains(od.ProductId))
                    .Select(od => new
                    {
                        od.ProductId,
                        productName = od.Product?.Name ?? "",
                        imageUrl    = od.Product?.ImageUrl ?? "",
                        od.Quantity,
                        od.UnitPrice
                    })
            });

            return Ok(new { items, total, page, limit });
        }

        // GET /api/orders/shop/{orderId}
        [HttpGet("shop/{orderId:int}")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetShopOrder(int orderId)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await _db.Products
                .Where(p => p.ShopId == shop!.Id)
                .Select(p => p.Id)
                .ToListAsync();

            var order = await _db.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == orderId &&
                    o.OrderDetails.Any(od => productIds.Contains(od.ProductId)));

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            return Ok(new
            {
                orderId         = order.Id,
                status          = order.Status,
                createdAt       = order.OrderDate,
                updatedAt       = order.UpdatedAt,
                cancelReason    = order.CancelReason,
                trackingCode    = order.TrackingCode,
                note            = order.Note,
                shippingAddress = order.ShippingAddress,
                totalAmount     = order.TotalAmount,
                customer        = new
                {
                    name  = order.User?.Name ?? "",
                    phone = order.User?.Phone ?? "",
                    email = order.User?.Email ?? ""
                },
                items = order.OrderDetails
                    .Where(od => productIds.Contains(od.ProductId))
                    .Select(od => new
                    {
                        od.ProductId,
                        productName = od.Product?.Name ?? "",
                        imageUrl    = od.Product?.ImageUrl ?? "",
                        od.Quantity,
                        od.UnitPrice,
                        subtotal    = od.UnitPrice * od.Quantity
                    })
            });
        }

        // PUT /api/orders/shop/{orderId}/confirm
        [HttpPut("shop/{orderId:int}/confirm")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> ConfirmOrder(int orderId)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await _db.Products.Where(p => p.ShopId == shop!.Id).Select(p => p.Id).ToListAsync();
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId &&
                    _db.OrderDetails.Any(od => od.OrderId == orderId && productIds.Contains(od.ProductId)));

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (order.Status != OrderStatus.Pending)
                return BadRequest(new { message = $"Chỉ có thể xác nhận đơn đang Pending, đơn này đang {order.Status}" });

            order.Status    = OrderStatus.Confirmed;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đơn hàng đã được xác nhận" });
        }

        // PUT /api/orders/shop/{orderId}/ship
        [HttpPut("shop/{orderId:int}/ship")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> ShipOrder(int orderId, [FromBody] ShipOrderDto dto)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var productIds = await _db.Products.Where(p => p.ShopId == shop!.Id).Select(p => p.Id).ToListAsync();
            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId &&
                    _db.OrderDetails.Any(od => od.OrderId == orderId && productIds.Contains(od.ProductId)));

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (order.Status != OrderStatus.Confirmed)
                return BadRequest(new { message = $"Chỉ có thể giao đơn đã xác nhận, đơn này đang {order.Status}" });

            order.Status       = OrderStatus.Shipping;
            order.TrackingCode = dto?.TrackingCode;
            order.UpdatedAt    = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đơn hàng đang được giao" });
        }

        // PUT /api/orders/shop/{orderId}/cancel
        [HttpPut("shop/{orderId:int}/cancel")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> CancelShopOrder(int orderId, [FromBody] CancelShopOrderDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(new { message = "Lý do hủy là bắt buộc" });

            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var productIds = await _db.Products.Where(p => p.ShopId == shop!.Id).Select(p => p.Id).ToListAsync();
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == orderId &&
                        o.OrderDetails.Any(od => productIds.Contains(od.ProductId)));

                if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
                if (order.Status != OrderStatus.Pending)
                    return BadRequest(new { message = "Chỉ có thể hủy đơn đang chờ xác nhận" });

                order.Status       = OrderStatus.Cancelled;
                order.CancelReason = dto.Reason;
                order.UpdatedAt    = DateTime.UtcNow;

                // Restock
                foreach (var detail in order.OrderDetails)
                {
                    var product = await _db.Products.FindAsync(detail.ProductId);
                    if (product != null) product.Stock += detail.Quantity;
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Đơn hàng đã bị hủy" });
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

    public class ShipOrderDto
    {
        public string? TrackingCode { get; set; }
    }

    public class CancelShopOrderDto
    {
        public string Reason { get; set; } = "";
    }
}