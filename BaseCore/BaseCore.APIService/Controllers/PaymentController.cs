using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly VNPayService _vnpay;
        private readonly IConfiguration _config;

        public PaymentController(MySqlDbContext db, VNPayService vnpay, IConfiguration config)
        {
            _db     = db;
            _vnpay  = vnpay;
            _config = config;
        }

        private string? GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        private string FrontendUrl =>
            _config["Frontend:BaseUrl"] ?? "http://127.0.0.1:5500/WebClient/WebClient/public/kaira-1.0.0";

        // ─────────────────────────────────────────────────────────────
        // VNPAY
        // ─────────────────────────────────────────────────────────────

        /// <summary>POST /api/payment/vnpay/create — Tạo URL thanh toán VNPay</summary>
        [HttpPost("vnpay/create")]
        [Authorize]
        public async Task<IActionResult> CreateVNPayUrl([FromBody] VNPayCreateDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == userId);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            if (order.PaymentStatus != PaymentStatusValue.WaitingPayment)
                return BadRequest(new { message = "Đơn hàng không ở trạng thái chờ thanh toán" });

            var ipAddr    = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var amount    = order.FinalAmount > 0 ? order.FinalAmount : order.TotalAmount + order.ShippingFee;
            var orderCode = order.OrderCode ?? ("ORD-" + order.Id.ToString("D6"));
            var orderInfo = $"Thanh toan {orderCode}";

            var paymentUrl = _vnpay.CreatePaymentUrl(order.Id, amount, orderInfo, ipAddr);
            return Ok(new { paymentUrl, orderId = order.Id, amount, orderCode });
        }

        /// <summary>GET /api/payment/vnpay/return — Redirect URL user thấy sau khi thanh toán</summary>
        [HttpGet("vnpay/return")]
        [AllowAnonymous]
        public async Task<IActionResult> VNPayReturn()
        {
            var queryParams = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

            if (!_vnpay.VerifySignature(queryParams))
                return Redirect($"{FrontendUrl}/order-result.html?status=error&message=invalid_signature");

            var txnRef       = Request.Query["vnp_TxnRef"].ToString();
            var responseCode = Request.Query["vnp_ResponseCode"].ToString();
            var transactionNo = Request.Query["vnp_TransactionNo"].ToString();

            if (!int.TryParse(txnRef, out var orderId))
                return Redirect($"{FrontendUrl}/order-result.html?status=error");

            var order = await _db.Orders.FindAsync(orderId);
            if (order != null && order.PaymentStatus == PaymentStatusValue.WaitingPayment)
            {
                order.PaymentStatus      = responseCode == "00" ? PaymentStatusValue.Paid : PaymentStatusValue.Failed;
                order.VNPayTransactionId = transactionNo;
                order.UpdatedAt          = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            var success = responseCode == "00";
            return Redirect($"{FrontendUrl}/order-result.html?status={(success ? "success" : "failed")}&orderId={orderId}");
        }

        /// <summary>GET /api/payment/vnpay/ipn — IPN server-to-server từ VNPay</summary>
        [HttpGet("vnpay/ipn")]
        [AllowAnonymous]
        public async Task<IActionResult> VNPayIPN()
        {
            var queryParams = Request.Query.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

            if (!_vnpay.VerifySignature(queryParams))
                return Ok(new { RspCode = "97", Message = "Invalid signature" });

            var txnRef        = Request.Query["vnp_TxnRef"].ToString();
            var responseCode  = Request.Query["vnp_ResponseCode"].ToString();
            var transactionNo = Request.Query["vnp_TransactionNo"].ToString();

            if (!int.TryParse(txnRef, out var orderId))
                return Ok(new { RspCode = "01", Message = "Order not found" });

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders.FindAsync(orderId);
                if (order == null)
                    return Ok(new { RspCode = "01", Message = "Order not found" });

                if (order.PaymentStatus == PaymentStatusValue.Paid)
                    return Ok(new { RspCode = "02", Message = "Order already confirmed" });

                if (responseCode == "00")
                {
                    order.PaymentStatus      = PaymentStatusValue.Paid;
                    order.VNPayTransactionId = transactionNo;
                    order.UpdatedAt          = DateTime.UtcNow;

                    _db.Notifications.Add(new Notification
                    {
                        UserId    = order.UserId,
                        Title     = "Thanh toán thành công",
                        Message   = $"Đơn hàng {order.OrderCode ?? ("ORD-" + order.Id)} đã được thanh toán qua VNPay.",
                        Link      = "/profile.html#orders",
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    order.PaymentStatus = PaymentStatusValue.Failed;
                    order.UpdatedAt     = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return Ok(new { RspCode = "00", Message = "Confirm Success" });
            }
            catch
            {
                await tx.RollbackAsync();
                return Ok(new { RspCode = "99", Message = "Internal error" });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // BANK TRANSFER
        // ─────────────────────────────────────────────────────────────

        /// <summary>GET /api/payment/bank/info?orderId= — Lấy thông tin chuyển khoản + QR</summary>
        [HttpGet("bank/info")]
        [Authorize]
        public async Task<IActionResult> GetBankInfo([FromQuery] int orderId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            var bankName      = _config["BankInfo:BankName"]      ?? "Vietcombank";
            var accountNumber = _config["BankInfo:AccountNumber"] ?? "";
            var accountName   = _config["BankInfo:AccountName"]   ?? "";
            var bankId        = _config["BankInfo:BankId"]        ?? "VCB";
            var amount        = (long)(order.FinalAmount > 0 ? order.FinalAmount : order.TotalAmount + order.ShippingFee);
            var orderCode     = order.OrderCode ?? ("ORD-" + order.Id.ToString("D6"));

            // VietQR URL
            var qrUrl = $"https://img.vietqr.io/image/{bankId}-{accountNumber}-compact2.jpg" +
                        $"?amount={amount}" +
                        $"&addInfo={Uri.EscapeDataString(orderCode)}" +
                        $"&accountName={Uri.EscapeDataString(accountName)}";

            return Ok(new
            {
                bankName,
                accountNumber,
                accountName,
                amount,
                orderCode,
                qrUrl,
                expireAt = order.PaymentExpireAt
            });
        }

        /// <summary>POST /api/payment/bank/submit — Khách xác nhận đã chuyển khoản</summary>
        [HttpPost("bank/submit")]
        [Authorize]
        public async Task<IActionResult> SubmitBankTransfer([FromBody] BankTransferDto dto)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == dto.OrderId && o.UserId == userId);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            if (order.PaymentMethod != PaymentMethodValue.Bank)
                return BadRequest(new { message = "Đơn hàng không dùng phương thức chuyển khoản" });

            if (order.PaymentStatus != PaymentStatusValue.WaitingPayment)
                return BadRequest(new { message = "Đơn hàng không ở trạng thái chờ thanh toán" });

            if (order.PaymentExpireAt.HasValue && order.PaymentExpireAt.Value < DateTime.UtcNow)
                return BadRequest(new { message = "Đơn hàng đã hết hạn thanh toán" });

            order.PaymentStatus          = PaymentStatusValue.PendingBankConfirm;
            order.BankTransferConfirmedAt = DateTime.UtcNow;
            order.UpdatedAt              = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Đã ghi nhận. Admin sẽ xác nhận trong vài phút." });
        }

        /// <summary>POST /api/payment/admin/bank/confirm/{orderId} — Admin xác nhận nhận tiền</summary>
        [HttpPost("admin/bank/confirm/{orderId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminConfirmBankPayment(int orderId)
        {
            var adminId = GetUserId();
            var order   = await _db.Orders.FindAsync(orderId);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            if (order.PaymentStatus != PaymentStatusValue.PendingBankConfirm)
                return BadRequest(new { message = "Đơn hàng không ở trạng thái chờ xác nhận chuyển khoản" });

            order.PaymentStatus          = PaymentStatusValue.Paid;
            order.BankTransferConfirmedBy = adminId;
            order.BankTransferConfirmedAt = DateTime.UtcNow;
            order.UpdatedAt              = DateTime.UtcNow;

            _db.Notifications.Add(new Notification
            {
                UserId    = order.UserId,
                Title     = "Đã xác nhận thanh toán",
                Message   = $"Đơn hàng {order.OrderCode ?? ("ORD-" + order.Id)} đã được xác nhận chuyển khoản thành công.",
                Link      = "/profile.html#orders",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xác nhận thanh toán chuyển khoản" });
        }

        /// <summary>GET /api/payment/admin/bank/pending — Admin xem danh sách đơn chờ xác nhận CK</summary>
        [HttpGet("admin/bank/pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingBankOrders()
        {
            var orders = await _db.Orders
                .Include(o => o.User)
                .Where(o => o.PaymentStatus == PaymentStatusValue.PendingBankConfirm)
                .OrderBy(o => o.BankTransferConfirmedAt)
                .Select(o => new {
                    orderId              = o.Id,
                    orderCode            = o.OrderCode ?? ("ORD-" + o.Id.ToString("D6")),
                    finalAmount          = o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount + o.ShippingFee,
                    confirmedAt          = o.BankTransferConfirmedAt,
                    expireAt             = o.PaymentExpireAt,
                    customerName         = o.User.Name,
                    customerEmail        = o.User.Email
                })
                .ToListAsync();

            return Ok(orders);
        }

        // ─────────────────────────────────────────────────────────────
        // AUTO-EXPIRE
        // ─────────────────────────────────────────────────────────────

        /// <summary>POST /api/payment/expire — Hủy các đơn WAITING_PAYMENT đã hết hạn</summary>
        [HttpPost("expire")]
        [AllowAnonymous]
        public async Task<IActionResult> ExpireOrders()
        {
            var now = DateTime.UtcNow;

            var expired = await _db.Orders
                .Where(o => o.PaymentStatus == PaymentStatusValue.WaitingPayment
                         && o.PaymentExpireAt.HasValue
                         && o.PaymentExpireAt.Value <= now)
                .ToListAsync();

            if (!expired.Any())
                return Ok(new { expired = 0 });

            var orderIds = expired.Select(o => o.Id).ToList();
            var details  = await _db.OrderDetails
                .Include(od => od.Product)
                .Where(od => orderIds.Contains(od.OrderId))
                .ToListAsync();

            foreach (var order in expired)
            {
                order.PaymentStatus = PaymentStatusValue.Expired;
                order.Status        = OrderStatus.Cancelled;
                order.CancelReason  = "Hết hạn thanh toán tự động";
                order.UpdatedAt     = now;

                foreach (var detail in details.Where(d => d.OrderId == order.Id))
                {
                    if (detail.Product != null) detail.Product.Stock += detail.Quantity;
                }

                _db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId   = order.Id,
                    Status    = OrderStatus.Cancelled,
                    Note      = "Hủy tự động: hết hạn thanh toán",
                    ChangedAt = now
                });
            }

            await _db.SaveChangesAsync();
            return Ok(new { expired = expired.Count });
        }
    }

    public class VNPayCreateDto
    {
        public int OrderId { get; set; }
    }

    public class BankTransferDto
    {
        public int OrderId { get; set; }
    }
}
