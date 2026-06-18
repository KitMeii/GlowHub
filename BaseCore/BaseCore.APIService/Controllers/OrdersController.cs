using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BaseCore.Common;
using BaseCore.Entities;
using BaseCore.Repository.EFCore;
using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.Data.SqlClient;
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
        private readonly ShippingCalculatorService _shipping;
        private readonly AuditLogService _audit;

        public OrdersController(
            IOrderRepositoryEF orderRepository,
            IOrderDetailRepositoryEF orderDetailRepository,
            IProductRepositoryEF productRepository,
            ICartRepositoryEF cartRepository,
            IShopRepositoryEF shopRepository,
            MySqlDbContext db,
            ShippingCalculatorService shipping,
            AuditLogService audit)
        {
            _orderRepository = orderRepository;
            _orderDetailRepository = orderDetailRepository;
            _productRepository = productRepository;
            _cartRepository = cartRepository;
            _shopRepository = shopRepository;
            _db = db;
            _shipping = shipping;
            _audit = audit;
        }

        private string? GetUserId()   => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

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
                    discount         = 0m,
                    finalAmount      = o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount + o.ShippingFee,
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
                .Include(o => o.SubOrders).ThenInclude(s => s.Items).ThenInclude(i => i.Product)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            var shopInfo = order.OrderDetails
                .Select(od => od.Product?.Shop)
                .Where(s => s != null)
                .Select(s => new { shopId = s!.Id, shopName = s.ShopName, logo = s.Logo })
                .FirstOrDefault();

            // Build shopId → shopName map from already-loaded OrderDetails
            var shopNameMap = order.OrderDetails
                .Where(od => od.Product?.Shop != null && od.Product.ShopId != null)
                .GroupBy(od => od.Product!.ShopId!)
                .ToDictionary(g => g.Key, g => g.First().Product!.Shop!.ShopName ?? "");

            return Ok(new {
                orderId               = order.Id,
                orderCode             = order.OrderCode ?? ("ORD-" + order.Id.ToString("D6")),
                status                = order.Status,
                totalAmount           = order.TotalAmount,
                shippingFee           = order.ShippingFee,
                discount              = order.SystemVoucherDiscount + order.ShopVoucherDiscount + order.FreeshipDiscount,
                systemVoucherDiscount = order.SystemVoucherDiscount,
                shopVoucherDiscount   = order.ShopVoucherDiscount,
                freeshipDiscount      = order.FreeshipDiscount,
                finalAmount           = order.FinalAmount > 0 ? order.FinalAmount : order.TotalAmount + order.ShippingFee,
                createdAt             = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc),
                updatedAt             = order.UpdatedAt != null ? (DateTime?)DateTime.SpecifyKind(order.UpdatedAt.Value, DateTimeKind.Utc) : null,
                shippingAddress       = order.ShippingAddress,
                receiverName          = order.ReceiverName,
                receiverPhone         = order.ReceiverPhone,
                trackingCode          = order.TrackingCode,
                cancelReason          = order.CancelReason,
                estimatedDelivery     = order.EstimatedDelivery != null ? (DateTime?)DateTime.SpecifyKind(order.EstimatedDelivery.Value, DateTimeKind.Utc) : null,
                paymentMethod         = order.PaymentMethod,
                paymentStatus         = order.PaymentStatus,
                note                  = order.Note,
                shopInfo,
                subOrders = order.SubOrders.OrderBy(s => s.Id).Select(s => new {
                    subOrderId          = s.Id,
                    shopId              = s.ShopId,
                    shopName            = s.ShopId != null && shopNameMap.TryGetValue(s.ShopId, out var sn) ? sn : "",
                    totalAmount         = s.TotalAmount,
                    shippingFee         = s.ShippingFee,
                    shopVoucherDiscount = s.ShopVoucherDiscount,
                    finalAmount         = s.FinalAmount,
                    status              = s.Status,
                    items = s.Items.Select(i => new {
                        productId   = i.ProductId,
                        productName = i.Product != null ? i.Product.Name : "",
                        imageUrl    = i.Product != null ? i.Product.ImageUrl : "",
                        qty         = i.Quantity,
                        unitPrice   = i.UnitPrice,
                        subtotal    = i.UnitPrice * i.Quantity
                    })
                }),
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

        /// <summary>POST /api/orders/my/{orderId}/received — Xác nhận đã nhận hàng (per-shop hoặc toàn đơn)</summary>
        [HttpPost("my/{orderId:int}/received")]
        public async Task<IActionResult> ConfirmReceived(int orderId, [FromBody] ConfirmReceivedDto? dto = null)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var order = await _db.Orders
                .Include(o => o.SubOrders)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

            if (order == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng" });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                if (dto?.SubOrderId.HasValue == true)
                {
                    // ── Per-shop confirm ───────────────────────────────────
                    var sub = order.SubOrders.FirstOrDefault(s => s.Id == dto.SubOrderId.Value);
                    if (sub == null) return NotFound(new { message = "Không tìm thấy SubOrder" });
                    if (sub.Status != OrderStatus.Shipping)
                        return BadRequest(new { message = "Shop này chưa ở trạng thái đang giao" });

                    sub.Status      = OrderStatus.Delivered;
                    sub.PayoutStatus = PayoutStatusValue.WaitingRelease;
                    sub.UpdatedAt   = DateTime.UtcNow;

                    // Check if all SubOrders are now DELIVERED
                    var allDelivered = order.SubOrders.All(s => s.Status == OrderStatus.Delivered);
                    if (allDelivered)
                    {
                        order.Status       = OrderStatus.Delivered;
                        order.PayoutStatus = PayoutStatusValue.WaitingRelease;
                        order.UpdatedAt    = DateTime.UtcNow;
                        _db.OrderStatusHistories.Add(new OrderStatusHistory {
                            OrderId   = orderId,
                            Status    = OrderStatus.Delivered,
                            Note      = "Khách xác nhận đã nhận hàng từ tất cả shop",
                            ChangedBy = userId,
                            ChangedAt = DateTime.UtcNow
                        });
                    }

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Ok(new {
                        message      = allDelivered ? "Bạn đã nhận đủ hàng từ tất cả shop!" : "Xác nhận nhận hàng từ shop thành công!",
                        allDelivered = allDelivered
                    });
                }
                else
                {
                    // ── Legacy: confirm toàn đơn ──────────────────────────
                    if (order.Status != OrderStatus.Shipping)
                        return BadRequest(new { message = "Chỉ xác nhận nhận hàng khi đơn đang giao" });

                    order.Status       = OrderStatus.Delivered;
                    order.PayoutStatus = PayoutStatusValue.WaitingRelease;
                    order.UpdatedAt    = DateTime.UtcNow;

                    _db.OrderStatusHistories.Add(new OrderStatusHistory {
                        OrderId   = orderId,
                        Status    = OrderStatus.Delivered,
                        Note      = "Khách xác nhận đã nhận hàng",
                        ChangedBy = userId,
                        ChangedAt = DateTime.UtcNow
                    });

                    await _db.SubOrders
                        .Where(s => s.OrderId == orderId)
                        .ExecuteUpdateAsync(s =>
                            s.SetProperty(x => x.Status,       OrderStatus.Delivered)
                             .SetProperty(x => x.PayoutStatus, PayoutStatusValue.WaitingRelease)
                             .SetProperty(x => x.UpdatedAt,    DateTime.UtcNow));

                    await _db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Ok(new { message = "Xác nhận nhận hàng thành công! Bạn có thể đánh giá sản phẩm.", allDelivered = true });
                }
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>GET /api/orders/my/{orderId}/track — Timeline 4 bước</summary>
        [HttpGet("my/{orderId:int}/track")]
        public async Task<IActionResult> GetTrackingTimeline(int orderId)
        {
            try
            {
                var userId = GetUserId();
                if (string.IsNullOrEmpty(userId)) return Unauthorized();

                var order = await _db.Orders
                    .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                    .Include(o => o.StatusHistory)
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
            catch (Exception ex)
            {
                return StatusCode(500, new {
                    error = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }
        }

        /// <summary>GET /api/orders/my/check-purchased/{productId} — kiểm tra user đã mua sản phẩm chưa</summary>
        [HttpGet("my/check-purchased/{productId:int}")]
        [Authorize]
        public async Task<IActionResult> CheckPurchased(int productId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var hasPurchased = await _db.SubOrderItems
                .AnyAsync(i => i.SubOrder.Order.UserId == userId && i.ProductId == productId);

            return Ok(new { hasPurchased });
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
        /// ★ CHECKOUT v3 — Multi-shop SubOrders + Dynamic ShippingFee + Voucher + Notify
        /// </summary>
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.ShippingAddress))
                return BadRequest(new { message = "Vui lòng nhập địa chỉ giao hàng." });

            var cartItems = await _db.CartItems
                .Include(c => c.Product).ThenInclude(p => p!.Shop)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest(new { message = "Giỏ hàng trống." });

            // Xác định vùng của khách hàng để tính phí ship
            var toRegion = ShippingRegion.Normalize(dto.ToProvince ?? dto.ShippingAddress);

            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // ── Validate & lock stock ────────────────────────────
                foreach (var cartItem in cartItems)
                {
                    var product = cartItem.Product;
                    if (product == null || !product.IsActive)
                        throw new Exception($"Sản phẩm #{cartItem.ProductId} không còn bán.");
                    if (product.Stock < cartItem.Quantity)
                        throw new Exception($"'{product.Name}' chỉ còn {product.Stock} sản phẩm, bạn đặt {cartItem.Quantity}.");
                }

                // ── Áp dụng voucher (system-level) ──────────────────
                decimal systemVoucherDiscount = 0m;
                decimal freeshipDiscount      = 0m;
                string? appliedVoucher        = null;

                if (!string.IsNullOrWhiteSpace(dto.VoucherCode))
                {
                    var voucher = await _db.Vouchers.FirstOrDefaultAsync(v =>
                        v.Code == dto.VoucherCode.ToUpper() && v.IsActive &&
                        (!v.ExpiryDate.HasValue || v.ExpiryDate >= DateTime.UtcNow) &&
                        (!v.StartDate.HasValue  || v.StartDate  <= DateTime.UtcNow) &&
                        (!v.UsageLimit.HasValue || v.UsedCount < v.UsageLimit));

                    if (voucher != null)
                    {
                        var subtotalAll = cartItems.Sum(c => (c.Product!.DiscountPrice ?? c.Product.Price) * c.Quantity);
                        if (subtotalAll >= voucher.MinOrderAmount && voucher.ShopId == null)
                        {
                            var disc = voucher.DiscountType == "percent"
                                ? subtotalAll * voucher.DiscountValue / 100
                                : voucher.DiscountValue;
                            if (voucher.MaxDiscount.HasValue && disc > voucher.MaxDiscount.Value)
                                disc = voucher.MaxDiscount.Value;
                            systemVoucherDiscount = disc;
                            voucher.UsedCount++;
                            appliedVoucher = voucher.Code;
                        }
                    }
                }

                // ── Tìm freeship voucher (áp dụng sau khi tính xong tổng ship) ──
                Voucher? shipVoucherEntity = null;
                if (!string.IsNullOrWhiteSpace(dto.ShipVoucherCode))
                {
                    shipVoucherEntity = await _db.Vouchers.FirstOrDefaultAsync(v =>
                        v.Code == dto.ShipVoucherCode.ToUpper() && v.IsActive &&
                        v.ShopId == null &&
                        (!v.ExpiryDate.HasValue || v.ExpiryDate >= DateTime.UtcNow) &&
                        (!v.StartDate.HasValue  || v.StartDate  <= DateTime.UtcNow) &&
                        (!v.UsageLimit.HasValue || v.UsedCount  < v.UsageLimit));
                }

                // ── Tạo parent Order ─────────────────────────────────
                var paymentMethod = dto.PaymentMethod switch {
                    1 => "BANK", 2 => "MOMO", 3 => "ZALOPAY", 4 => "VNPAY", _ => "COD"
                };
                var needsOnlinePayment = paymentMethod is "VNPAY" or "BANK";
                int deliveryDays = dto.ShippingMethod == "same" ? 1 : dto.ShippingMethod == "express" ? 2 : 5;

                var order = new Order {
                    UserId                = userId,
                    OrderDate             = DateTime.UtcNow,
                    Status                = OrderStatus.Pending,
                    PayoutStatus          = PayoutStatusValue.Pending,
                    PaymentMethod         = paymentMethod,
                    PaymentStatus         = needsOnlinePayment ? PaymentStatusValue.WaitingPayment : PaymentStatusValue.Unpaid,
                    PaymentExpireAt       = needsOnlinePayment ? DateTime.UtcNow.AddMinutes(15) : null,
                    ToProvince            = dto.ToProvince,
                    ShippingAddress       = dto.ShippingAddress,
                    ReceiverName          = dto.ReceiverName ?? "",
                    ReceiverPhone         = dto.ReceiverPhone ?? "",
                    Note                  = dto.Note,
                    SystemVoucherDiscount = systemVoucherDiscount,
                    FreeshipDiscount      = freeshipDiscount,
                    EstimatedDelivery     = DateTime.UtcNow.AddDays(deliveryDays)
                };
                _db.Orders.Add(order);
                await _db.SaveChangesAsync(); // cần Id

                order.OrderCode = "ORD-" + order.Id.ToString("D6");

                // ── Tạo SubOrders theo từng shop ─────────────────────
                // Group by ShopId (empty string for products without a shop)
                var shopGroups = cartItems.GroupBy(c => c.Product!.ShopId ?? "");
                decimal grandTotal     = 0m;
                decimal grandSubtotal  = 0m;
                decimal grandShipping  = 0m;
                decimal grandSellerPayout = 0m;
                int subOrderSeq        = 1;

                var subOrderResults          = new List<object>();
                decimal grandShopVoucherDiscount = 0m;

                foreach (var group in shopGroups)
                {
                    var shop      = group.First().Product!.Shop;
                    var hasShop   = shop != null && !string.IsNullOrEmpty(group.Key);
                    var fromRegion = ShippingRegion.Normalize(shop?.Region ?? shop?.Province);

                    // Tính shipping fee động theo trọng lượng + vùng
                    int groupWeight = group.Sum(c => (c.Product!.WeightGram) * c.Quantity);
                    var shipResult  = _shipping.Calculate(fromRegion, toRegion, groupWeight);
                    decimal shipFee = dto.ShippingMethod == "express" ? shipResult.Fee + 15_000m
                                    : dto.ShippingMethod == "same"    ? shipResult.Fee + 30_000m
                                    : shipResult.Fee;

                    // Financial per shop group
                    decimal groupSubtotal = group.Sum(c => (c.Product!.DiscountPrice ?? c.Product.Price) * c.Quantity);

                    // ── Áp dụng voucher của shop (nếu có) ──────────────
                    decimal shopVoucherDisc    = 0m;
                    string? appliedShopVoucher = null;
                    if (hasShop && dto.ShopVouchers != null &&
                        dto.ShopVouchers.TryGetValue(group.Key, out var svCode) &&
                        !string.IsNullOrWhiteSpace(svCode))
                    {
                        var sv = await _db.Vouchers.FirstOrDefaultAsync(v =>
                            v.Code == svCode.ToUpper() && v.IsActive &&
                            v.ShopId == group.Key &&
                            (!v.ExpiryDate.HasValue || v.ExpiryDate >= DateTime.UtcNow) &&
                            (!v.StartDate.HasValue  || v.StartDate  <= DateTime.UtcNow) &&
                            (!v.UsageLimit.HasValue || v.UsedCount  <  v.UsageLimit));
                        if (sv != null && groupSubtotal >= sv.MinOrderAmount)
                        {
                            var d = sv.DiscountType == "percent"
                                ? groupSubtotal * sv.DiscountValue / 100
                                : sv.DiscountValue;
                            if (sv.MaxDiscount.HasValue && d > sv.MaxDiscount.Value) d = sv.MaxDiscount.Value;
                            shopVoucherDisc    = d;
                            sv.UsedCount++;
                            appliedShopVoucher = sv.Code;
                        }
                    }

                    decimal commRate      = shop?.CommissionRate > 0 ? shop!.CommissionRate : 10m;
                    decimal productRev    = groupSubtotal - shopVoucherDisc;
                    decimal commAmt       = Math.Round(productRev * commRate / 100m, 2);
                    decimal sellerPayout  = productRev - commAmt;
                    decimal groupFinal    = groupSubtotal + shipFee - shopVoucherDisc;

                    // Tạo SubOrder chỉ khi sản phẩm thuộc về một shop hợp lệ
                    SubOrder? subOrder = null;
                    if (hasShop)
                    {
                        subOrder = new SubOrder {
                            OrderId             = order.Id,
                            ShopId              = shop!.Id,
                            Status              = OrderStatus.Pending,
                            TotalAmount         = groupSubtotal,
                            ShippingFee         = shipFee,
                            FinalAmount         = groupFinal,
                            ProductRevenue      = productRev,
                            CommissionRate      = commRate,
                            CommissionAmount    = commAmt,
                            SellerPayoutAmount  = sellerPayout,
                            ShopVoucherDiscount = shopVoucherDisc,
                            PayoutStatus        = PayoutStatusValue.Pending,
                            Note                = dto.Note,
                            CreatedAt           = DateTime.UtcNow
                        };
                        _db.SubOrders.Add(subOrder);
                        await _db.SaveChangesAsync();
                        subOrder.SubOrderCode = "SUB-" + order.Id.ToString("D6") + "-" + subOrderSeq++;
                    }

                    // Tạo SubOrderItems (khi có SubOrder) + deduct stock + OrderDetails
                    foreach (var cartItem in group)
                    {
                        var product = cartItem.Product!;
                        decimal lockedPrice = product.DiscountPrice ?? product.Price;

                        if (hasShop && subOrder != null)
                        {
                            _db.SubOrderItems.Add(new SubOrderItem {
                                SubOrderId = subOrder.Id,
                                ProductId  = product.Id,
                                Quantity   = cartItem.Quantity,
                                UnitPrice  = lockedPrice
                            });
                        }

                        // OrderDetail (backward compat với legacy endpoints)
                        _db.OrderDetails.Add(new OrderDetail {
                            OrderId   = order.Id,
                            ProductId = product.Id,
                            Quantity  = cartItem.Quantity,
                            UnitPrice = lockedPrice
                        });

                        // ── Atomic stock decrement — chống race condition ──
                        // Nếu 2 khách cùng mua sản phẩm cuối cùng, chỉ 1 UPDATE thành công.
                        // Pre-check ở trên (line 503) chỉ để báo lỗi sớm với message đẹp;
                        // đây mới là rào chắn thực sự.
                        var stockRows = await _db.Database.ExecuteSqlRawAsync(
                            @"UPDATE Products
                              SET Stock     = Stock - @qty,
                                  SoldCount = SoldCount + @qty
                              WHERE Id = @pid AND IsActive = 1 AND Stock >= @qty",
                            new SqlParameter("@qty", cartItem.Quantity),
                            new SqlParameter("@pid", product.Id));
                        if (stockRows == 0)
                            throw new Exception($"'{product.Name}' đã hết hàng hoặc số lượng tồn không đủ.");
                    }

                    grandSubtotal            += groupSubtotal;
                    grandShipping            += shipFee;
                    grandTotal               += groupFinal;
                    grandShopVoucherDiscount += shopVoucherDisc;
                    grandSellerPayout        += sellerPayout;

                    if (hasShop && subOrder != null)
                    {
                        subOrderResults.Add(new {
                            subOrderId          = subOrder.Id,
                            shopId              = subOrder.ShopId,
                            shopName            = shop!.ShopName ?? "GlowHub",
                            subtotal            = groupSubtotal,
                            shippingFee         = shipFee,
                            shopVoucherDiscount = shopVoucherDisc,
                            appliedShopVoucher,
                            finalAmount         = groupFinal
                        });

                        // Notify seller
                        _db.Notifications.Add(new Notification {
                            UserId    = shop!.SellerId,
                            Title     = "Đơn hàng mới",
                            Message   = $"SubOrder {subOrder.SubOrderCode} - {groupFinal:N0}₫",
                            Link      = "/seller-dashboard.html",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // ── Áp dụng freeship voucher ──────────────────────────────
                if (shipVoucherEntity != null)
                {
                    var subtotalForShip = cartItems.Sum(c => (c.Product!.DiscountPrice ?? c.Product.Price) * c.Quantity);
                    if (subtotalForShip >= shipVoucherEntity.MinOrderAmount)
                    {
                        var disc = shipVoucherEntity.DiscountType == "percent"
                            ? grandShipping * shipVoucherEntity.DiscountValue / 100
                            : shipVoucherEntity.DiscountValue;
                        if (shipVoucherEntity.MaxDiscount.HasValue && disc > shipVoucherEntity.MaxDiscount.Value)
                            disc = shipVoucherEntity.MaxDiscount.Value;
                        freeshipDiscount = Math.Min(disc, grandShipping);
                        shipVoucherEntity.UsedCount++;
                    }
                }

                // ── Cập nhật Order tổng ──────────────────────────────
                order.TotalAmount           = grandSubtotal;
                order.ShippingFee           = grandShipping;
                order.ShopVoucherDiscount   = grandShopVoucherDiscount;
                order.SystemVoucherDiscount = systemVoucherDiscount;
                order.FreeshipDiscount      = freeshipDiscount;
                order.Discount              = systemVoucherDiscount + grandShopVoucherDiscount + freeshipDiscount;
                order.FinalAmount           = grandTotal - systemVoucherDiscount - freeshipDiscount;
                order.SellerPayoutAmount    = grandSellerPayout;
                order.ShopId               = null; // order spans multiple shops

                // Xóa giỏ hàng
                _db.CartItems.RemoveRange(cartItems);

                // Status history
                _db.OrderStatusHistories.Add(new OrderStatusHistory {
                    OrderId   = order.Id,
                    Status    = OrderStatus.Pending,
                    Note      = "Đơn hàng được tạo",
                    ChangedBy = userId,
                    ChangedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Audit log — checkout là sự kiện tài chính, phải log để truy vết khi có khiếu nại
                try
                {
                    await _audit.Log(userId, GetUserName(), "ORDER_CREATE", "Order", order.Id.ToString(),
                        newValue: new {
                            orderCode = order.OrderCode,
                            finalAmount = order.FinalAmount,
                            paymentMethod,
                            subOrderCount = subOrderResults.Count,
                            appliedVoucher,
                            appliedShipVoucher = shipVoucherEntity?.Code
                        },
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
                }
                catch { /* không để audit fail ngăn response */ }

                return Ok(new {
                    message                  = "Đặt hàng thành công!",
                    orderId                  = order.Id,
                    orderCode                = order.OrderCode,
                    totalAmount              = grandSubtotal,
                    totalShipping            = grandShipping,
                    discount                 = systemVoucherDiscount,
                    shopVoucherDiscount      = grandShopVoucherDiscount,
                    freeshipDiscount         = freeshipDiscount,
                    appliedShipVoucher       = shipVoucherEntity?.Code,
                    totalDiscount            = systemVoucherDiscount + grandShopVoucherDiscount + freeshipDiscount,
                    finalAmount              = order.FinalAmount,
                    subOrders                = subOrderResults,
                    estimatedDelivery = DateTime.SpecifyKind(order.EstimatedDelivery!.Value, DateTimeKind.Utc),
                    appliedVoucher,
                    paymentMethod,
                    paymentStatus     = order.PaymentStatus,
                    paymentExpireAt   = order.PaymentExpireAt.HasValue
                        ? (DateTime?)DateTime.SpecifyKind(order.PaymentExpireAt.Value, DateTimeKind.Utc) : null,
                    requiresPayment   = needsOnlinePayment
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
                var msg = ex.Message;
                if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
                if (ex.InnerException?.InnerException != null) msg += " | " + ex.InnerException.InnerException.Message;
                return BadRequest(new { message = msg });
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
        // ADMIN — full order management
        // ─────────────────────────────────────────────────────────

        /// <summary>GET /api/admin/orders?status=&amp;search=&amp;from=&amp;to=&amp;page=&amp;limit=</summary>
        [HttpGet("~/api/admin/orders")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminOrders(
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            [FromQuery] string? from   = null,
            [FromQuery] string? to     = null,
            [FromQuery] string? payment = null,
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 20)
        {
            var query = _db.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status.ToUpper() != "ALL")
                query = query.Where(o => o.Status == status.ToUpper());

            if (!string.IsNullOrEmpty(payment))
                query = query.Where(o => o.PaymentMethod == payment.ToUpper());

            if (!string.IsNullOrEmpty(search))
                query = query.Where(o =>
                    (o.OrderCode != null && o.OrderCode.Contains(search)) ||
                    (o.ReceiverName != null && o.ReceiverName.Contains(search)) ||
                    (o.ReceiverPhone != null && o.ReceiverPhone.Contains(search)) ||
                    o.User.Name.Contains(search) ||
                    o.User.Email.Contains(search));

            if (DateTime.TryParse(from, out var fromDate))
                query = query.Where(o => o.OrderDate >= fromDate.ToUniversalTime());

            if (DateTime.TryParse(to, out var toDate))
                query = query.Where(o => o.OrderDate <= toDate.ToUniversalTime().AddDays(1));

            var total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(o => new {
                    orderId       = o.Id,
                    orderCode     = o.OrderCode ?? ("ORD-" + o.Id.ToString("D6")),
                    status        = o.Status,
                    paymentMethod = o.PaymentMethod,
                    paymentStatus = o.PaymentStatus,
                    totalAmount   = o.TotalAmount,
                    finalAmount   = o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount + o.ShippingFee,
                    shippingFee   = o.ShippingFee,
                    createdAt     = DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc),
                    receiverName  = o.ReceiverName,
                    receiverPhone = o.ReceiverPhone,
                    shippingAddress = o.ShippingAddress,
                    cancelReason  = o.CancelReason,
                    trackingCode  = o.TrackingCode,
                    itemCount     = o.OrderDetails.Count,
                    customer = new {
                        id    = o.User.Id,
                        name  = o.User.Name,
                        email = o.User.Email
                    },
                    firstItem = o.OrderDetails.Select(od => new {
                        productName = od.Product != null ? od.Product.Name : "",
                        imageUrl    = od.Product != null ? od.Product.ImageUrl : ""
                    }).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new { items = orders, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        /// <summary>GET /api/admin/orders/{id} — Chi tiết đơn hàng cho admin</summary>
        [HttpGet("~/api/admin/orders/{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminOrderDetail(int id)
        {
            var order = await _db.Orders
                .Include(o => o.User)
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product).ThenInclude(p => p == null ? null : p.Shop)
                .Include(o => o.SubOrders).ThenInclude(so => so.Shop)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            return Ok(new {
                orderId         = order.Id,
                orderCode       = order.OrderCode ?? ("ORD-" + order.Id.ToString("D6")),
                status          = order.Status,
                paymentMethod   = order.PaymentMethod,
                paymentStatus   = order.PaymentStatus,
                totalAmount     = order.TotalAmount,
                shippingFee     = order.ShippingFee,
                finalAmount     = order.FinalAmount > 0 ? order.FinalAmount : order.TotalAmount + order.ShippingFee,
                shippingAddress = order.ShippingAddress,
                receiverName    = order.ReceiverName,
                receiverPhone   = order.ReceiverPhone,
                trackingCode    = order.TrackingCode,
                cancelReason    = order.CancelReason,
                note            = order.Note,
                createdAt       = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc),
                updatedAt       = order.UpdatedAt != null ? (DateTime?)DateTime.SpecifyKind(order.UpdatedAt.Value, DateTimeKind.Utc) : null,
                customer = new {
                    id    = order.User.Id,
                    name  = order.User.Name,
                    email = order.User.Email,
                    phone = order.User.Phone
                },
                items = order.OrderDetails.Select(od => new {
                    productId   = od.ProductId,
                    productName = od.Product?.Name ?? "",
                    imageUrl    = od.Product?.ImageUrl ?? "",
                    shopName    = od.Product?.Shop?.ShopName ?? "",
                    qty         = od.Quantity,
                    unitPrice   = od.UnitPrice,
                    subtotal    = od.UnitPrice * od.Quantity
                }),
                systemVoucherDiscount = order.SystemVoucherDiscount,
                freeshipDiscount      = order.FreeshipDiscount,
                subOrders = order.SubOrders
                    .OrderBy(so => so.ShopId)
                    .Select(so => new {
                        shopId              = so.ShopId,
                        shopName            = so.Shop != null ? so.Shop.ShopName : so.ShopId,
                        status              = so.Status,
                        totalAmount         = so.TotalAmount,
                        shippingFee         = so.ShippingFee,
                        shopVoucherDiscount = so.ShopVoucherDiscount,
                        finalAmount         = so.FinalAmount,
                        items = order.OrderDetails
                            .Where(od => od.Product != null && od.Product.ShopId == so.ShopId)
                            .Select(od => new {
                                productName = od.Product!.Name,
                                qty         = od.Quantity,
                                unitPrice   = od.UnitPrice,
                                subtotal    = od.UnitPrice * od.Quantity
                            })
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

        /// <summary>PUT /api/admin/orders/{id}/status — Admin cập nhật trạng thái (có ghi lịch sử)</summary>
        [HttpPut("~/api/admin/orders/{id:int}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminUpdateOrderStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            var validStatuses = new[] {
                OrderStatus.Pending, OrderStatus.Confirmed,
                OrderStatus.Shipping, OrderStatus.Delivered,
                OrderStatus.Completed, OrderStatus.Cancelled
            };
            if (!validStatuses.Contains(dto.Status))
                return BadRequest(new { message = "Trạng thái không hợp lệ" });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

                var oldStatus = order.Status;
                order.Status    = dto.Status;
                order.UpdatedAt = DateTime.UtcNow;

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

                _db.OrderStatusHistories.Add(new OrderStatusHistory {
                    OrderId   = id,
                    Status    = dto.Status,
                    Note      = dto.Note ?? $"Admin cập nhật: {oldStatus} → {dto.Status}",
                    ChangedBy = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier),
                    ChangedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new { message = $"Đã cập nhật đơn #{id} → {dto.Status}" });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
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

        // Sync parent Order.Status from all its SubOrders (SubOrder is source of truth per shop)
        private void SyncOrderStatus(Order order)
        {
            var statuses = order.SubOrders.Select(s => s.Status).ToList();
            if (!statuses.Any()) return;
            if (statuses.All(s => s == OrderStatus.Delivered))
                order.Status = OrderStatus.Delivered;
            else if (statuses.Any(s => s == OrderStatus.Shipping))
                order.Status = OrderStatus.Shipping;
            else if (statuses.Any(s => s == OrderStatus.Confirmed))
                order.Status = OrderStatus.Confirmed;
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

        // ─────────────────────────────────────────────────────────
        // SELLER — SubOrder endpoints (Sprint 11)
        // ─────────────────────────────────────────────────────────

        /// <summary>GET /api/orders/shop/suborders?status=&amp;page=&amp;limit= — Seller xem SubOrders của shop</summary>
        [HttpGet("shop/suborders")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> GetShopSubOrders(
            [FromQuery] string? status = null,
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 10)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var query = _db.SubOrders
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .Include(s => s.Order).ThenInclude(o => o.User)
                .Where(s => s.ShopId == shop!.Id)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.Status == status.ToUpper());

            var total  = await query.CountAsync();
            var orders = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(s => new {
                    subOrderId      = s.Id,
                    subOrderCode    = s.SubOrderCode ?? ("SUB-" + s.Id.ToString("D6")),
                    orderId         = s.OrderId,
                    orderCode       = s.Order.OrderCode ?? ("ORD-" + s.OrderId.ToString("D6")),
                    status          = s.Status,
                    payoutStatus    = s.PayoutStatus,
                    totalAmount     = s.TotalAmount,
                    shippingFee     = s.ShippingFee,
                    finalAmount     = s.FinalAmount,
                    sellerPayout        = s.SellerPayoutAmount,
                    shopVoucherDiscount = s.ShopVoucherDiscount,
                    trackingCode        = s.TrackingCode,
                    cancelReason    = s.CancelReason,
                    createdAt       = s.CreatedAt,
                    shippingAddress = s.Order.ShippingAddress,
                    receiverName    = s.Order.ReceiverName,
                    receiverPhone   = s.Order.ReceiverPhone,
                    customer = new {
                        name  = s.Order.User.Name,
                        phone = s.Order.User.Phone,
                        email = s.Order.User.Email
                    },
                    items = s.Items.Select(i => new {
                        productId   = i.ProductId,
                        productName = i.Product != null ? i.Product.Name : "",
                        imageUrl    = i.Product != null ? i.Product.ImageUrl : "",
                        quantity    = i.Quantity,
                        unitPrice   = i.UnitPrice,
                        subtotal    = i.UnitPrice * i.Quantity
                    })
                })
                .ToListAsync();

            return Ok(new { items = orders, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        /// <summary>PUT /api/orders/shop/suborders/{id}/confirm — Seller xác nhận SubOrder</summary>
        [HttpPut("shop/suborders/{id:int}/confirm")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> ConfirmSubOrder(int id)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var sub = await _db.SubOrders.FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shop!.Id);
            if (sub == null) return NotFound(new { message = "Không tìm thấy SubOrder" });
            if (sub.Status != OrderStatus.Pending)
                return BadRequest(new { message = $"SubOrder đang ở trạng thái {sub.Status}" });

            sub.Status    = OrderStatus.Confirmed;
            sub.UpdatedAt = DateTime.UtcNow;

            var order = await _db.Orders
                .Include(o => o.SubOrders)
                .FirstOrDefaultAsync(o => o.Id == sub.OrderId);
            if (order != null) { SyncOrderStatus(order); order.UpdatedAt = DateTime.UtcNow; }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xác nhận SubOrder" });
        }

        /// <summary>PUT /api/orders/shop/suborders/{id}/ship — Seller bắt đầu giao SubOrder</summary>
        [HttpPut("shop/suborders/{id:int}/ship")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> ShipSubOrder(int id, [FromBody] ShipOrderDto dto)
        {
            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            var sub = await _db.SubOrders.FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shop!.Id);
            if (sub == null) return NotFound(new { message = "Không tìm thấy SubOrder" });
            if (sub.Status != OrderStatus.Confirmed)
                return BadRequest(new { message = $"SubOrder chưa được xác nhận" });

            sub.Status       = OrderStatus.Shipping;
            sub.TrackingCode = dto?.TrackingCode;
            sub.UpdatedAt    = DateTime.UtcNow;

            var order = await _db.Orders
                .Include(o => o.SubOrders)
                .FirstOrDefaultAsync(o => o.Id == sub.OrderId);
            if (order != null)
            {
                SyncOrderStatus(order);
                var shippingCount = order.SubOrders.Count(s => s.Status == OrderStatus.Shipping);
                order.TrackingCode = shippingCount <= 1 ? dto?.TrackingCode : "Nhiều đơn vị vận chuyển";
                order.UpdatedAt    = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "SubOrder đang được giao" });
        }

        /// <summary>PUT /api/orders/shop/suborders/{id}/cancel — Seller hủy SubOrder</summary>
        [HttpPut("shop/suborders/{id:int}/cancel")]
        [Authorize(Roles = RoleConstant.Seller)]
        public async Task<IActionResult> CancelSubOrder(int id, [FromBody] CancelShopOrderDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(new { message = "Lý do hủy là bắt buộc" });

            var (shop, err) = await GetActiveShopAsync();
            if (err != null) return err;

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var sub = await _db.SubOrders
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == id && s.ShopId == shop!.Id);

                if (sub == null) return NotFound(new { message = "Không tìm thấy SubOrder" });
                if (sub.Status != OrderStatus.Pending)
                    return BadRequest(new { message = "Chỉ hủy được SubOrder đang Pending" });

                sub.Status       = OrderStatus.Cancelled;
                sub.CancelReason = dto.Reason;
                sub.UpdatedAt    = DateTime.UtcNow;

                foreach (var item in sub.Items)
                {
                    var product = await _db.Products.FindAsync(item.ProductId);
                    if (product != null) product.Stock += item.Quantity;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return Ok(new { message = "Đã hủy SubOrder" });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>POST /api/orders/my/{orderId}/received — Khách xác nhận nhận hàng → tất cả SubOrders WAITING_RELEASE</summary>
        // (Override lại endpoint đã có ở trên để sync SubOrders)

    }

    public class ConfirmReceivedDto
    {
        public int? SubOrderId { get; set; }
    }

    public class CheckoutDto
    {
        public string ShippingAddress { get; set; } = "";
        public string? Note { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        /// <summary>Tỉnh/thành của khách — dùng tính phí ship động</summary>
        public string? ToProvince { get; set; }
        /// <summary>0=COD, 1=Bank, 2=MoMo, 3=ZaloPay</summary>
        public int PaymentMethod { get; set; } = 0;
        public string? VoucherCode { get; set; }
        /// <summary>Voucher giảm/miễn phí vận chuyển</summary>
        public string? ShipVoucherCode { get; set; }
        /// <summary>Voucher riêng từng shop: key=shopId, value=voucherCode</summary>
        public Dictionary<string, string>? ShopVouchers { get; set; }
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
        public string? Note { get; set; }
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