using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    // Tách hẳn các thao tác Admin duyệt chuyển khoản ra khỏi PaymentController:
    // PaymentController giữ phần khách (info/submit + VNPay), controller này lo phần admin.
    [Route("api/admin/bank-confirmation")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class BankConfirmationController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;

        public BankConfirmationController(MySqlDbContext db, AuditLogService audit)
        {
            _db    = db;
            _audit = audit;
        }

        private string? GetUserId()   => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

        /// <summary>GET /api/admin/bank-confirmation/pending — Đơn khách báo CK đang chờ admin xác nhận</summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPending()
        {
            var orders = await _db.Orders
                .Include(o => o.User)
                .Where(o => o.PaymentStatus == PaymentStatusValue.PendingBankConfirm)
                .OrderBy(o => o.BankTransferConfirmedAt)
                .Select(o => new {
                    orderId       = o.Id,
                    orderCode     = o.OrderCode ?? ("ORD-" + o.Id.ToString("D6")),
                    finalAmount   = o.FinalAmount > 0 ? o.FinalAmount : o.TotalAmount + o.ShippingFee,
                    confirmedAt   = o.BankTransferConfirmedAt,
                    expireAt      = o.PaymentExpireAt,
                    customerName  = o.User.Name,
                    customerEmail = o.User.Email
                })
                .ToListAsync();

            return Ok(orders);
        }

        /// <summary>POST /api/admin/bank-confirmation/{orderId}/confirm — Admin xác nhận đã nhận tiền</summary>
        [HttpPost("{orderId:int}/confirm")]
        public async Task<IActionResult> Confirm(int orderId)
        {
            var adminId = GetUserId();
            var order   = await _db.Orders.FindAsync(orderId);
            if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

            if (order.PaymentStatus != PaymentStatusValue.PendingBankConfirm)
                return BadRequest(new { message = "Đơn hàng không ở trạng thái chờ xác nhận chuyển khoản" });

            var oldStatus = order.PaymentStatus;
            order.PaymentStatus           = PaymentStatusValue.Paid;
            order.BankTransferConfirmedBy = adminId;
            order.BankTransferConfirmedAt = DateTime.UtcNow;
            order.UpdatedAt               = DateTime.UtcNow;

            _db.Notifications.Add(new Notification
            {
                UserId    = order.UserId,
                Title     = "Đã xác nhận thanh toán",
                Message   = $"Đơn hàng {order.OrderCode ?? ("ORD-" + order.Id)} đã được xác nhận chuyển khoản thành công.",
                Link      = "/profile.html#orders",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            try
            {
                await _audit.Log(adminId, GetUserName(), "PAYMENT_BANK_CONFIRM", "Order", order.Id.ToString(),
                    oldValue: new { paymentStatus = oldStatus.ToString() },
                    newValue: new { paymentStatus = order.PaymentStatus.ToString(), amount = order.FinalAmount },
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
            }
            catch { }

            return Ok(new { message = "Đã xác nhận thanh toán chuyển khoản" });
        }

        /// <summary>POST /api/admin/bank-confirmation/{orderId}/reject — Admin từ chối: hủy đơn + hoàn kho + audit</summary>
        [HttpPost("{orderId:int}/reject")]
        public async Task<IActionResult> Reject(int orderId, [FromBody] RejectBankDto? dto)
        {
            var adminId = GetUserId();
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng" });

                if (order.PaymentStatus != PaymentStatusValue.PendingBankConfirm)
                    return BadRequest(new { message = "Đơn không ở trạng thái chờ xác nhận chuyển khoản" });

                var oldStatus  = order.Status;
                var oldPayment = order.PaymentStatus;
                var note       = string.IsNullOrWhiteSpace(dto?.Note)
                    ? "Admin từ chối xác nhận chuyển khoản"
                    : dto!.Note.Trim();

                order.PaymentStatus = PaymentStatusValue.Failed;
                order.Status        = OrderStatus.Cancelled;
                order.CancelReason  = note;
                order.UpdatedAt     = DateTime.UtcNow;

                foreach (var detail in order.OrderDetails)
                {
                    var product = await _db.Products.FindAsync(detail.ProductId);
                    if (product != null) product.Stock += detail.Quantity;
                }

                _db.OrderStatusHistories.Add(new OrderStatusHistory {
                    OrderId   = order.Id,
                    Status    = OrderStatus.Cancelled,
                    Note      = note,
                    ChangedBy = adminId,
                    ChangedAt = DateTime.UtcNow
                });

                _db.Notifications.Add(new Notification {
                    UserId    = order.UserId,
                    Title     = "Đơn hàng đã bị hủy",
                    Message   = $"Đơn hàng {order.OrderCode ?? ("ORD-" + order.Id)} bị hủy: {note}",
                    Link      = "/profile.html#orders",
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                try
                {
                    await _audit.Log(adminId, GetUserName(), "PAYMENT_BANK_REJECT",
                        "Order", order.Id.ToString(),
                        oldValue: new { status = oldStatus, paymentStatus = oldPayment.ToString() },
                        newValue: new { status = order.Status, paymentStatus = order.PaymentStatus.ToString(), note },
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
                }
                catch { }

                return Ok(new { message = "Đã từ chối và hủy đơn" });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class RejectBankDto
    {
        public string? Note { get; set; }
    }
}
