using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/disputes")]
    [ApiController]
    [Authorize]
    public class DisputeController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;
        private readonly NotificationService _notify;

        public DisputeController(MySqlDbContext db, AuditLogService audit, NotificationService notify)
        {
            _db     = db;
            _audit  = audit;
            _notify = notify;
        }

        private string? GetUserId()   => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
        private bool IsAdmin() => User.IsInRole("Admin");

        // ─────────────────────────────────────────────────────────
        // GET /api/disputes/my — Khiếu nại của customer hiện tại
        // ─────────────────────────────────────────────────────────
        [HttpGet("my")]
        public async Task<IActionResult> GetMy([FromQuery] int page = 1, [FromQuery] int limit = 20)
        {
            var userId = GetUserId();
            var query = _db.Disputes
                .Include(d => d.Order)
                .Where(d => d.CustomerId == userId)
                .OrderByDescending(d => d.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(d => new {
                    d.Id,
                    d.OrderId,
                    orderCode  = d.Order != null ? d.Order.OrderCode : null,
                    d.Reason,
                    d.Description,
                    d.Status,
                    d.RefundAmount,
                    d.FavorCustomer,
                    d.Resolution,
                    d.CreatedAt,
                    d.ResolvedAt
                })
                .ToListAsync();

            return Ok(new { items, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/disputes — Tất cả khiếu nại (Admin)
        // ─────────────────────────────────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? status = null,
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 20)
        {
            var query = _db.Disputes
                .Include(d => d.Order)
                .Include(d => d.Customer)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.Status == status.ToUpper());

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(d => new {
                    d.Id,
                    d.OrderId,
                    orderCode    = d.Order != null ? d.Order.OrderCode : null,
                    d.CustomerId,
                    customerName = d.Customer != null ? d.Customer.Name : null,
                    d.Reason,
                    d.Description,
                    d.Status,
                    d.RefundAmount,
                    d.FavorCustomer,
                    d.Resolution,
                    d.CreatedAt,
                    d.ResolvedAt,
                    d.ResolvedBy
                })
                .ToListAsync();

            return Ok(new { items, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/disputes/{id} — Chi tiết khiếu nại
        // ─────────────────────────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var d = await _db.Disputes
                .Include(x => x.Order).ThenInclude(o => o!.OrderDetails).ThenInclude(od => od.Product)
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return NotFound(new { message = "Không tìm thấy khiếu nại" });

            var userId = GetUserId();
            if (!IsAdmin() && d.CustomerId != userId)
                return Forbid();

            return Ok(new {
                d.Id,
                d.OrderId,
                orderCode    = d.Order?.OrderCode,
                orderStatus  = d.Order?.Status,
                d.CustomerId,
                customerName = d.Customer?.Name,
                customerEmail= d.Customer?.Email,
                d.Reason,
                d.Description,
                d.Evidence,
                d.Status,
                d.RefundAmount,
                d.FavorCustomer,
                d.Resolution,
                d.CreatedAt,
                d.ResolvedAt,
                d.ResolvedBy,
                orderDetails = d.Order?.OrderDetails?.Select(od => new {
                    od.ProductId,
                    productName = od.Product?.Name,
                    od.Quantity,
                    od.UnitPrice
                })
            });
        }

        // ─────────────────────────────────────────────────────────
        // POST /api/disputes — Customer tạo khiếu nại
        // ─────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateDisputeDto dto)
        {
            var userId = GetUserId()!;

            var order = await _db.Orders.FindAsync(dto.OrderId);
            if (order == null)
                return NotFound(new { message = "Không tìm thấy đơn hàng" });

            if (order.UserId != userId)
                return Forbid();

            if (order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Shipping && order.Status != OrderStatus.Completed)
                return BadRequest(new { message = "Chỉ có thể khiếu nại đơn hàng đang giao, đã giao hoặc đã hoàn thành" });

            var exists = await _db.Disputes.AnyAsync(d => d.OrderId == dto.OrderId && d.CustomerId == userId && d.Status != DisputeStatus.Resolved);
            if (exists)
                return BadRequest(new { message = "Bạn đã có khiếu nại đang xử lý cho đơn hàng này" });

            var dispute = new Dispute
            {
                OrderId     = dto.OrderId,
                CustomerId  = userId,
                Reason      = dto.Reason,
                Description = dto.Description,
                Evidence    = dto.Evidence,
                Status      = DisputeStatus.Open,
                CreatedAt   = DateTime.UtcNow
            };

            _db.Disputes.Add(dispute);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = dispute.Id }, new {
                message   = "Khiếu nại đã được gửi thành công",
                disputeId = dispute.Id,
                status    = dispute.Status
            });
        }

        // ─────────────────────────────────────────────────────────
        // PUT /api/disputes/{id}/process — Admin: OPEN → PROCESSING
        // ─────────────────────────────────────────────────────────
        [HttpPut("{id:int}/process")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Process(int id)
        {
            var dispute = await _db.Disputes.Include(d => d.Order).FirstOrDefaultAsync(d => d.Id == id);
            if (dispute == null) return NotFound(new { message = "Không tìm thấy khiếu nại" });

            if (dispute.Status != DisputeStatus.Open)
                return BadRequest(new { message = "Chỉ có thể xử lý khiếu nại ở trạng thái OPEN" });

            var oldStatus = dispute.Status;
            dispute.Status = DisputeStatus.Processing;
            await _db.SaveChangesAsync();

            await _audit.Log(GetUserId(), GetUserName(), "DISPUTE_PROCESS",
                "Dispute", id.ToString(),
                new { status = oldStatus },
                new { status = DisputeStatus.Processing });

            await _notify.CreateAsync(dispute.CustomerId,
                NotificationType.SystemAlert,
                "Khiếu nại đang được xử lý",
                $"Khiếu nại #{id} của bạn đang được admin xem xét.",
                "/profile.html?tab=disputes");

            return Ok(new { message = "Đã chuyển khiếu nại sang trạng thái đang xử lý", status = dispute.Status });
        }

        // ─────────────────────────────────────────────────────────
        // PUT /api/disputes/{id}/resolve — Admin: PROCESSING → RESOLVED
        // ─────────────────────────────────────────────────────────
        [HttpPut("{id:int}/resolve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Resolve(int id, [FromBody] ResolveDisputeDto dto)
        {
            var dispute = await _db.Disputes.Include(d => d.Order).FirstOrDefaultAsync(d => d.Id == id);
            if (dispute == null) return NotFound(new { message = "Không tìm thấy khiếu nại" });

            if (dispute.Status == DisputeStatus.Resolved)
                return BadRequest(new { message = "Khiếu nại đã được giải quyết" });

            if (dto.RefundAmount < 0)
                return BadRequest(new { message = "Số tiền hoàn trả không hợp lệ" });

            var oldStatus = dispute.Status;
            dispute.Status        = DisputeStatus.Resolved;
            dispute.Resolution    = dto.Resolution;
            dispute.RefundAmount  = dto.RefundAmount;
            dispute.FavorCustomer = dto.FavorCustomer;
            dispute.ResolvedAt    = DateTime.UtcNow;
            dispute.ResolvedBy    = GetUserId();

            await _db.SaveChangesAsync();

            await _audit.Log(GetUserId(), GetUserName(), "DISPUTE_RESOLVE",
                "Dispute", id.ToString(),
                new { status = oldStatus },
                new { status = DisputeStatus.Resolved, refundAmount = dto.RefundAmount, favorCustomer = dto.FavorCustomer });

            var notifyMsg = dto.FavorCustomer
                ? $"Khiếu nại #{id} đã được giải quyết theo hướng có lợi cho bạn. Hoàn tiền: {dto.RefundAmount:N0}₫"
                : $"Khiếu nại #{id} đã được giải quyết. {dto.Resolution}";

            await _notify.CreateAsync(dispute.CustomerId,
                NotificationType.SystemAlert,
                "Khiếu nại đã được giải quyết",
                notifyMsg,
                "/profile.html?tab=disputes");

            return Ok(new {
                message       = "Đã giải quyết khiếu nại",
                status        = dispute.Status,
                refundAmount  = dispute.RefundAmount,
                favorCustomer = dispute.FavorCustomer
            });
        }
    }

    public class CreateDisputeDto
    {
        public int OrderId { get; set; }
        public string Reason { get; set; } = "";
        public string? Description { get; set; }
        public string? Evidence { get; set; }
    }

    public class ResolveDisputeDto
    {
        public string Resolution { get; set; } = "";
        public decimal RefundAmount { get; set; }
        public bool FavorCustomer { get; set; }
    }
}
