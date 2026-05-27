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

        /// <summary>Lấy đơn hàng của user đang đăng nhập (đầy đủ details + tên sản phẩm)</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var orders = await _db.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.UpdatedAt,
                    o.TotalAmount,
                    o.Status,
                    o.ShippingAddress,
                    o.Note,
                    o.CustomerName,
                    o.CustomerEmail,
                    o.CustomerPhone,
                    o.ShippingFee,
                    o.PaymentMethod,
                    o.VoucherCode,
                    o.DiscountAmount,
                    OrderDetails = o.OrderDetails.Select(od => new
                    {
                        od.Id,
                        od.ProductId,
                        ProductName = od.Product.Name,
                        ProductImage = od.Product.ImageUrl,
                        od.Quantity,
                        od.UnitPrice
                    })
                })
                .ToListAsync();
            return Ok(orders);
        }

        /// <summary>Chi tiết 1 đơn hàng kèm sản phẩm (user chỉ xem được đơn của mình)</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetUserId();
            var isAdmin = User.IsInRole("Admin");

            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });
            if (!isAdmin && order.UserId != userId)
                return Forbid();

            return Ok(new
            {
                order.Id,
                order.UserId,
                order.OrderDate,
                order.UpdatedAt,
                order.TotalAmount,
                order.Status,
                order.ShippingAddress,
                order.Note,
                order.CustomerName,
                order.CustomerEmail,
                order.CustomerPhone,
                order.ShippingFee,
                order.PaymentMethod,
                order.VoucherCode,
                order.DiscountAmount,
                OrderDetails = order.OrderDetails.Select(od => new
                {
                    od.Id,
                    od.ProductId,
                    ProductName = od.Product.Name,
                    ProductImage = od.Product.ImageUrl,
                    od.Quantity,
                    od.UnitPrice
                })
            });
        }

        /// <summary>Tất cả đơn hàng — chỉ Admin</summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _db.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.UserId,
                    UserName = o.User.Name,
                    o.OrderDate,
                    o.UpdatedAt,
                    o.TotalAmount,
                    o.Status,
                    o.ShippingAddress,
                    o.Note,
                    o.CustomerName,
                    o.CustomerEmail,
                    o.CustomerPhone,
                    o.ShippingFee,
                    o.PaymentMethod,
                    o.VoucherCode,
                    o.DiscountAmount,
                    OrderDetails = o.OrderDetails.Select(od => new
                    {
                        od.Id,
                        od.ProductId,
                        ProductName = od.Product.Name,
                        od.Quantity,
                        od.UnitPrice
                    })
                })
                .ToListAsync();
            return Ok(orders);
        }

        /// <summary>
        /// ★ CHECKOUT — Sacred Checkout với Transaction ACID
        /// Kiểm kho → trừ kho → tạo đơn → áp voucher (tăng UsedCount) → xóa giỏ
        /// Toàn bộ trong 1 Transaction. Lỗi bất kỳ bước → Rollback.
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
                decimal subtotal = 0;
                var orderDetails = new List<OrderDetail>();

                foreach (var cartItem in cartItems)
                {
                    var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == cartItem.ProductId);

                    if (product == null || !product.IsActive)
                        throw new Exception($"Sản phẩm '{cartItem.Product?.Name}' không còn bán.");

                    if (product.Stock < cartItem.Quantity)
                        throw new Exception($"'{product.Name}' chỉ còn {product.Stock}, bạn đặt {cartItem.Quantity}.");

                    decimal lockedPrice = product.Price;
                    subtotal += lockedPrice * cartItem.Quantity;
                    product.Stock -= cartItem.Quantity;

                    orderDetails.Add(new OrderDetail
                    {
                        ProductId = product.Id,
                        Quantity = cartItem.Quantity,
                        UnitPrice = lockedPrice
                    });
                }

                // ★ Áp voucher (nếu có) — tăng UsedCount trong cùng transaction
                decimal discountAmount = 0;
                string? appliedVoucherCode = null;
                if (!string.IsNullOrWhiteSpace(dto.VoucherCode))
                {
                    var code = dto.VoucherCode.Trim().ToUpperInvariant();
                    var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Code == code);
                    if (voucher == null || !voucher.IsActive)
                        throw new Exception("Mã giảm giá không tồn tại hoặc đã ngừng hoạt động.");
                    if (voucher.StartDate.HasValue && voucher.StartDate.Value > DateTime.UtcNow)
                        throw new Exception("Mã giảm giá chưa đến thời gian sử dụng.");
                    if (voucher.ExpiryDate.HasValue && voucher.ExpiryDate.Value < DateTime.UtcNow)
                        throw new Exception("Mã giảm giá đã hết hạn.");
                    if (voucher.UsageLimit.HasValue && voucher.UsedCount >= voucher.UsageLimit.Value)
                        throw new Exception("Mã giảm giá đã hết lượt sử dụng.");
                    if (subtotal < voucher.MinOrderAmount)
                        throw new Exception($"Đơn tối thiểu {voucher.MinOrderAmount:N0}₫ để áp mã này.");

                    discountAmount = voucher.DiscountType == "percent"
                        ? subtotal * voucher.DiscountValue / 100m
                        : voucher.DiscountValue;
                    if (voucher.MaxDiscount.HasValue && discountAmount > voucher.MaxDiscount.Value)
                        discountAmount = voucher.MaxDiscount.Value;
                    if (discountAmount > subtotal) discountAmount = subtotal;

                    voucher.UsedCount += 1;
                    appliedVoucherCode = voucher.Code;
                }

                decimal shippingFee = dto.ShippingFee < 0 ? 0 : dto.ShippingFee;
                decimal totalAmount = subtotal - discountAmount + shippingFee;
                if (totalAmount < 0) totalAmount = 0;

                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    Status = OrderStatus.Pending,
                    ShippingAddress = dto.ShippingAddress,
                    Note = dto.Note ?? "",
                    CustomerName = dto.CustomerName,
                    CustomerEmail = dto.CustomerEmail,
                    CustomerPhone = dto.CustomerPhone,
                    ShippingFee = shippingFee,
                    PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "COD" : dto.PaymentMethod,
                    VoucherCode = appliedVoucherCode,
                    DiscountAmount = discountAmount,
                    OrderDetails = orderDetails
                };
                _db.Orders.Add(order);

                _db.CartItems.RemoveRange(cartItems);

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Đặt hàng thành công!", orderId = order.Id, totalAmount, discountAmount, shippingFee });
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

        /// <summary>User tự hủy đơn hàng của chính mình — chỉ khi đang PENDING</summary>
        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelMyOrder(int id, [FromBody] CancelOrderDto? dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
                if (order.UserId != userId) return Forbid();
                if (order.Status != OrderStatus.Pending)
                    return BadRequest(new { message = "Chỉ có thể hủy đơn đang chờ xử lý." });

                var oldStatus = order.Status;
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.Now;

                // Hoàn kho
                foreach (var detail in order.OrderDetails)
                {
                    var product = await _db.Products.FindAsync(detail.ProductId);
                    if (product != null) product.Stock += detail.Quantity;
                }

                // Ghi log
                var note = string.IsNullOrWhiteSpace(dto?.Note) ? "User tự hủy đơn" : dto!.Note;
                var createdAt = dto?.ClientCreatedAt?.ToLocalTime() ?? DateTime.Now;
                _db.OrderStatusLogs.Add(new OrderStatusLog
                {
                    OrderId = id,
                    OldStatus = oldStatus,
                    NewStatus = OrderStatus.Cancelled,
                    ChangedBy = userId,
                    Note = note,
                    CreatedAt = createdAt
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Đã hủy đơn hàng thành công." });
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
                order.UpdatedAt = DateTime.Now;

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

                // ★ LOG: ghi nhật ký thay đổi trạng thái (chỉ khi status thực sự thay đổi)
                if (oldStatus != dto.Status)
                {
                    var changedBy = User.FindFirst("UserId")?.Value
                                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                    ?? "system";
                    // Ưu tiên giờ thực tế trên máy client (admin đang thao tác);
                    // fallback sang giờ local của server nếu client không gửi.
                    var createdAt = dto.ClientCreatedAt?.ToLocalTime() ?? DateTime.Now;
                    _db.OrderStatusLogs.Add(new OrderStatusLog
                    {
                        OrderId = id,
                        OldStatus = oldStatus,
                        NewStatus = dto.Status,
                        ChangedBy = changedBy,
                        Note = dto.Note,
                        CreatedAt = createdAt
                    });
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
        public string? CustomerName  { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal ShippingFee   { get; set; }
        public string? PaymentMethod { get; set; }
        public string? VoucherCode   { get; set; }
    }

    public class CancelOrderDto
    {
        public string? Note { get; set; }
        public DateTime? ClientCreatedAt { get; set; }
    }

    public class UpdateStatusDto
    {
        public string Status { get; set; } = "";
        public string? Note { get; set; }
        /// <summary>Giờ thực tế trên máy admin khi bấm cập nhật (UTC ISO). Optional.</summary>
        public DateTime? ClientCreatedAt { get; set; }
    }
}
