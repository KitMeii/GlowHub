using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Repository;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    /// <summary>
    /// Nhật ký thay đổi trạng thái đơn hàng — Admin xem tất cả, user xem đơn của mình.
    /// Log được ghi tự động bởi OrdersController.UpdateStatus và OrdersController.CancelMyOrder.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class OrderStatusLogsController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        public OrderStatusLogsController(MySqlDbContext db) { _db = db; }

        /// <summary>Tất cả log gần đây — phân trang (chỉ Admin)</summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] int? orderId = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 50;

            var q = _db.OrderStatusLogs.AsQueryable();
            if (orderId.HasValue) q = q.Where(l => l.OrderId == orderId.Value);

            var total = await q.CountAsync();
            var rawLogs = await q
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();

            var changerIds = rawLogs.Select(l => l.ChangedBy).Distinct().ToList();
            var userMap = await _db.Users
                .Where(u => changerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.UserName })
                .ToDictionaryAsync(u => u.Id, u => new { u.Name, u.UserName });

            var items = rawLogs.Select(l => new
            {
                l.Id,
                l.OrderId,
                l.OldStatus,
                l.NewStatus,
                l.Note,
                l.CreatedAt,
                ChangedBy = l.ChangedBy,
                ChangedByName = userMap.TryGetValue(l.ChangedBy, out var u) ? (u.Name ?? u.UserName ?? "(Admin)") : "(Admin)"
            });

            return Ok(new { items, totalCount = total, page, pageSize });
        }

        /// <summary>Log của 1 đơn cụ thể — Admin hoặc chủ đơn xem được</summary>
        [HttpGet("by-order/{orderId:int}")]
        [Authorize]
        public async Task<IActionResult> GetByOrder(int orderId)
        {
            // Verify quyền: Admin hoặc user là chủ đơn
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin)
            {
                var order = await _db.Orders.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                if (order == null) return NotFound(new { message = "Không tìm thấy đơn hàng." });
                if (order.UserId != currentUserId) return Forbid();
            }

            var rawLogs = await _db.OrderStatusLogs
                .Where(l => l.OrderId == orderId)
                .OrderBy(l => l.CreatedAt)
                .ToListAsync();

            var changerIds = rawLogs.Select(l => l.ChangedBy).Distinct().ToList();
            var userMap = await _db.Users
                .Where(u => changerIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.UserName })
                .ToDictionaryAsync(u => u.Id, u => new { u.Name, u.UserName });

            var items = rawLogs.Select(l => new
            {
                l.Id, l.OrderId, l.OldStatus, l.NewStatus, l.Note, l.CreatedAt,
                ChangedBy = l.ChangedBy,
                ChangedByName = userMap.TryGetValue(l.ChangedBy, out var u) ? (u.Name ?? u.UserName ?? "(Admin)") : "(Admin)"
            });
            return Ok(items);
        }
    }
}
