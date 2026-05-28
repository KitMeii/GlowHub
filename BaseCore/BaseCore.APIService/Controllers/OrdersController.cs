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

        // ─────────────────────────────────────────────────────────
        // CUSTOMER — my/ routes (Sprint 6)
        // ─────────────────────────────────────────────────────────

        /// <summary>GET /api/orders/my — danh sách đơn hàng có phân trang + lọc</summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyOrdersPaged(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var query = _db.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Where(o => o.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status.ToUpper() != "ALL")
                query = query.Where(o => o.Status == status.ToUpper());

            var total = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(o => new {
                    orderId          = o.Id,
                    orderCode        = o.OrderCode ?? ("ORD-" + o.Id.ToString("D6")),
                    status           = o.Status,
                    totalAmount      = o.TotalAmount,
                    shippingFee      = o.ShippingFee,
                    discount         = o.Discount,
                    finalAmount      = o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount + o.ShippingFee - o.Discount,
                    createdAt        = DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc),
                    updatedAt        = o.UpdatedAt != null ? (DateTime?)DateTime.SpecifyKind(o.UpdatedAt.Value, DateTimeKind.Utc) : null,
                    shippingAddress  = o.ShippingAddress,
                    receiverName     = o.ReceiverName,
                    receiverPhone    = o.ReceiverPhone,
                    trackingCode     = o.TrackingCode,
                    cancelReason     = o.CancelReason,
                    estimatedDelivery = o.EstimatedDelivery != null ? (DateTime?)DateTime.SpecifyKind(o.EstimatedDelivery.Value, DateTimeKind.Utc) : null,
                    paymentMethod    = o.PaymentMethod,
                    paymentStatus    = o.PaymentStatus,
                    itemCount        = o.OrderDetails.Count,
                    firstItem        = o.OrderDetails.Select(od => new {
                        productName = od.Product != null ? od.Product.Name : "",
                        imageUrl    = od.Product != null ? od.Product.ImageUrl : "",
                        qty         = od.Quantity,
                        unitPrice   = od.UnitPrice
                    }).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new {
                items = orders,
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit)
            });
        }

        /// <summary>GET /api/orders/my/{orderId} — chi tiết đơn hàng kèm timeline</summary>
        [HttpGet("my/{orderId:int}")]
        public async Task<IActionResult> GetMyOrderDetail(int orderId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var order = await _db.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product).ThenInclude(p => p == null ? null : p.Shop)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            var shopInfo = order.OrderDetails
                .Select(od => od.Product?.Shop)
                .Where(s => s != null)
                .Select(s => new { shopId = s!.Id, shopName = s.ShopName, logo = s.Logo })
                .FirstOrDefault();

            return Ok(new {
                orderId          = order.Id,
                orderCode        = order.OrderCode ?? ("ORD-" + order.Id.ToString("D6")),
                status           = order.Status,
                totalAmount      = order.TotalAmount,
                shippingFee      = order.ShippingFee,
                discount         = order.Discount,
                finalAmount      = order.FinalAmount > 0 ? order.FinalAmount : order.TotalAmount + order.ShippingFee - order.Discount,
                createdAt        = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc),
                updatedAt        = order.UpdatedAt != null ? (DateTime?)DateTime.SpecifyKind(order.UpdatedAt.Value, DateTimeKind.Utc) : null,
                shippingAddress  = order.ShippingAddress,
                receiverName     = order.ReceiverName,
                receiverPhone    = order.ReceiverPhone,
                trackingCode     = order.TrackingCode,
                cancelReason     = order.CancelReason,
                estimatedDelivery = order.EstimatedDelivery != null ? (DateTime?)DateTime.SpecifyKind(order.EstimatedDelivery.Value, DateTimeKind.Utc) : null,
                paymentMethod    = order.PaymentMethod,
                paymentStatus    = order.PaymentStatus,
                note             = order.Note,
                shopInfo,
                items = order.OrderDetails.Select(od => new {
                    productId   = od.ProductId,
                    productName = od.Product?.Name ?? "",
                    imageUrl    = od.Product?.ImageUrl ?? "",
                    qty         = od.Quantity,
                    unitPrice   = od.UnitPrice,
                    subtotal    = od.UnitPrice * od.Quantity
                }),
                statusHistory = order.StatusHistory
                    .OrderBy(h => h.ChangedAt)
                    .Select(h => new {
                        status    = h.Status,
                        note      = h.Note,
                        changedAt = DateTime.SpecifyKind(h.ChangedAt, DateTimeKind.Utc)
                    })
            });
        }

        /// <summary>POST /api/orders/my/{orderId}/cancel — Customer hủy đơn (chỉ Pending, bắt buộc lý do)</summary>
        [HttpPost("my/{orderId:int}/cancel")]
        public async Task<IActionResult> CancelMyOrderV2(int orderId, [FromBody] CancelOrderDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(new { message = "Lý do hủy là bắt buộc" });

            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                    return NotFound(new { message = "Không tìm thấy đơn hàng" });

                if (order.Status != OrderStatus.Pending)
                    return BadRequest(new { message = "Chỉ có thể hủy đơn đang chờ xác nhận (Pending)" });

                order.Status       = OrderStatus.Cancelled;
                order.CancelReason = dto.Reason;
                order.UpdatedAt    = DateTime.UtcNow;

                // Restock
                foreach (var detail in order.OrderDetails)
                {
                    var product = await _db.Products.FindAsync(detail.ProductId);
                    if (product != null) product.Stock += detail.Quantity;
                }

                // Status history
                _db.OrderStatusHistories.Add(new OrderStatusHistory {
                    OrderId   = orderId,
                    Status    = OrderStatus.Cancelled,
                    Note      = "Khách hủy: " + dto.Reason,
                    ChangedBy = userId,
                    ChangedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new { message = "Đã hủy đơn hàng thành công" });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>POST /api/orders/my/{orderId}/received — Xác nhận đã nhận hàng (Shipping → Delivered)</summary>
        [HttpPost("my/{orderId:int}/received")]
        public async Task<IActionResult> ConfirmReceived(int orderId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng" });

            if (order.Status != OrderStatus.Shipping)
                return BadRequest(new { message = "Chỉ xác nhận nhận hàng khi đơn đang giao" });

            order.Status    = OrderStatus.Delivered;
            order.UpdatedAt = DateTime.UtcNow;

            _db.OrderStatusHistories.Add(new OrderStatusHistory {
                OrderId   = orderId,
                Status    = OrderStatus.Delivered,
                Note      = "Khách xác nhận đã nhận hàng",
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { message = "Xác nhận nhận hàng thành công! Bạn có thể đánh giá sản phẩm." });
        }

        /// <summary>GET /api/orders/my/{orderId}/track — Timeline 4 bước</summary>
        [HttpGet("my/{orderId:int}/track")]
        public async Task<IActionResult> GetTrackingTimeline(int orderId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var order = await _db.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.StatusHistory.OrderBy(h => h.ChangedAt))
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            var steps = new[] {
                OrderStatus.Pending, OrderStatus.Confirmed,
                OrderStatus.Shipping, OrderStatus.Delivered
            };

            var statusOrder = new[] {
                OrderStatus.Pending, OrderStatus.Confirmed,
                OrderStatus.Shipping, OrderStatus.Delivered, OrderStatus.Completed
            };

            var currentIdx = Array.IndexOf(statusOrder, order.Status);
            if (order.Status == OrderStatus.Cancelled) currentIdx = -1;

            var timeline = new object[4];
            var stepLabels = new[] { "Đặt hàng thành công", "Shop xác nhận", "Đang giao hàng", "Đã nhận hàng" };
            var stepDescs  = new[] {
                "Đơn hàng đã được đặt thành công",
                "Shop đã xác nhận và chuẩn bị hàng",
                "Đơn hàng đang trên đường giao đến bạn",
                "Đơn hàng đã được giao thành công"
            };

            for (int i = 0; i < 4; i++)
            {
                var history = order.StatusHistory.FirstOrDefault(h => h.Status == steps[i]);
                var isCompleted = currentIdx >= i && currentIdx >= 0;
                var isCurrent   = currentIdx == i;
                timeline[i] = new {
                    step        = i + 1,
                    status      = steps[i],
                    label       = stepLabels[i],
                    description = stepDescs[i],
                    isCompleted,
                    isCurrent,
                    timestamp   = history != null
                        ? (DateTime?)DateTime.SpecifyKind(history.ChangedAt, DateTimeKind.Utc)
                        : (i == 0 ? (DateTime?)DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc) : null)
                };
            }

            return Ok(new {
                orderId          = order.Id,
                orderCode        = order.OrderCode ?? ("ORD-" + order.Id.ToString("D6")),
                currentStatus    = order.Status,
                isCancelled      = order.Status == OrderStatus.Cancelled,
                cancelReason     = order.CancelReason,
                trackingCode     = order.TrackingCode,
                estimatedDelivery = order.EstimatedDelivery != null
                    ? (DateTime?)DateTime.SpecifyKind(order.EstimatedDelivery.Value, DateTimeKind.Utc) : null,
                timeline,
                items = order.OrderDetails.Select(od => new {
                    productName = od.Product?.Name ?? "",
                    imageUrl    = od.Product?.ImageUrl ?? "",
                    qty         = od.Quantity,
                    unitPrice   = od.UnitPrice
                })
            });
        }

        // ─────────────────────────────────────────────────────────
        // LEGACY — kept for backward compat
        // ─────────────────────────────────────────────────────────

        /// <summary>Lấy đơn hàng của user đang đăng nhập (legacy)</summary>
        [HttpGet]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var orders = await _orderRepository.GetByUserAsync(userId);
            return Ok(orders);
        }

        /// <summary>Chi tiết 1 đơn hàng kèm sản phẩm (legacy)</summary>
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
        /// ★ CHECKOUT v2 — Transaction ACID + OrderCode + ShippingFee + Voucher + Notify
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
                string? shopId = null;

                foreach (var cartItem in cartItems)
                {
                    var product = await _db.Products
                        .Include(p => p.Shop)
                        .FirstOrDefaultAsync(p => p.Id == cartItem.ProductId);

                    if (product == null || !product.IsActive)
                        throw new Exception($"Sản phẩm '{cartItem.Product?.Name ?? cartItem.ProductId.ToString()}' không còn bán.");

                    if (product.Stock < cartItem.Quantity)
                        throw new Exception($"'{product.Name}' chỉ còn {product.Stock} sản phẩm, bạn đặt {cartItem.Quantity}.");

                    decimal lockedPrice = product.DiscountPrice ?? product.Price;
                    totalAmount += lockedPrice * cartItem.Quantity;

                    product.Stock     -= cartItem.Quantity;
                    product.SoldCount += cartItem.Quantity;

                    if (shopId == null && product.ShopId != null) shopId = product.ShopId;

                    orderDetails.Add(new OrderDetail {
                        ProductId = product.Id,
                        Quantity  = cartItem.Quantity,
                        UnitPrice = lockedPrice
                    });
                }

                // Tính phí ship: miễn phí nếu đơn ≥ 500k
                decimal shippingFee = totalAmount >= 500000m ? 0m : 30000m;
                if (dto.ShippingMethod == "express") shippingFee = 30000m;
                if (dto.ShippingMethod == "same")    shippingFee = 50000m;
                if (totalAmount >= 500000m && dto.ShippingMethod == "standard") shippingFee = 0m;

                // Áp dụng voucher nếu có
                decimal discount = 0m;
                string? appliedVoucher = null;
                if (!string.IsNullOrWhiteSpace(dto.VoucherCode))
                {
                    var voucher = await _db.Vouchers.FirstOrDefaultAsync(v =>
                        v.Code == dto.VoucherCode.ToUpper() && v.IsActive &&
                        (!v.ExpiryDate.HasValue || v.ExpiryDate >= DateTime.UtcNow) &&
                        (!v.StartDate.HasValue  || v.StartDate  <= DateTime.UtcNow) &&
                        (!v.UsageLimit.HasValue || v.UsedCount < v.UsageLimit));

                    if (voucher != null && totalAmount >= voucher.MinOrderAmount)
                    {
                        discount = voucher.DiscountType == "percent"
                            ? totalAmount * voucher.DiscountValue / 100
                            : voucher.DiscountValue;
                        if (voucher.MaxDiscount.HasValue && discount > voucher.MaxDiscount.Value)
                            discount = voucher.MaxDiscount.Value;
                        voucher.UsedCount++;
                        appliedVoucher = voucher.Code;
                    }
                }

                decimal finalAmount = totalAmount + shippingFee - discount;

                var order = new Order {
                    UserId          = userId,
                    OrderDate       = DateTime.UtcNow,
                    TotalAmount     = totalAmount,
                    ShippingFee     = shippingFee,
                    Discount        = discount,
                    FinalAmount     = finalAmount,
                    Status          = OrderStatus.Pending,
                    PaymentMethod   = dto.PaymentMethod,
                    PaymentStatus   = 0,
                    ShippingAddress = dto.ShippingAddress,
                    ReceiverName    = dto.ReceiverName ?? "",
                    ReceiverPhone   = dto.ReceiverPhone ?? "",
                    Note            = dto.Note,
                    EstimatedDelivery = DateTime.UtcNow.AddDays(dto.ShippingMethod == "same" ? 1 : dto.ShippingMethod == "express" ? 2 : 5),
                    OrderDetails    = orderDetails
                };
                _db.Orders.Add(order);

                // Xóa giỏ hàng
                _db.CartItems.RemoveRange(cartItems);

                await _db.SaveChangesAsync();

                // Tạo OrderCode sau khi có Id
                order.OrderCode = "ORD-" + order.Id.ToString("D6");

                // Status history khởi tạo
                _db.OrderStatusHistories.Add(new OrderStatusHistory {
                    OrderId   = order.Id,
                    Status    = OrderStatus.Pending,
                    Note      = "Đơn hàng được tạo",
                    ChangedBy = userId,
                    ChangedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notify Seller (async, không chặn)
                if (shopId != null)
                {
                    var shop = await _db.Shops.FindAsync(shopId);
                    if (shop != null)
                    {
                        _db.Notifications.Add(new Notification {
                            UserId    = shop.SellerId,
                            Title     = "Đơn hàng mới",
                            Message   = $"Bạn có đơn hàng mới {order.OrderCode} - {finalAmount:N0}₫",
                            Link      = "/seller-dashboard.html",
                            CreatedAt = DateTime.UtcNow
                        });
                        await _db.SaveChangesAsync();
                    }
                }

                return Ok(new {
                    message           = "Đặt hàng thành công!",
                    orderId           = order.Id,
                    orderCode         = order.OrderCode,
                    totalAmount,
                    shippingFee,
                    discount,
                    finalAmount,
                    estimatedDelivery = DateTime.SpecifyKind(order.EstimatedDelivery!.Value, DateTimeKind.Utc),
                    appliedVoucher
                });
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
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        /// <summary>0=COD, 1=Bank, 2=MoMo, 3=ZaloPay</summary>
        public int PaymentMethod { get; set; } = 0;
        public string? VoucherCode { get; set; }
        /// <summary>standard | express | same</summary>
        public string ShippingMethod { get; set; } = "standard";
    }

    public class CancelOrderDto
    {
        public string Reason { get; set; } = "";
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