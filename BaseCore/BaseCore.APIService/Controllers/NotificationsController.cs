using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BaseCore.Repository;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/notifications")]
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        public NotificationsController(MySqlDbContext db) => _db = db;

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // GET /api/notifications/my?page=1&limit=20&isRead=false
        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20,
            [FromQuery] bool? isRead = null)
        {
            var userId = GetUserId()!;
            var query  = _db.Notifications.Where(n => n.UserId == userId);

            if (isRead.HasValue) query = query.Where(n => n.IsRead == isRead.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(n => new {
                    n.Id, n.Type, n.Title, n.Message, n.IsRead, n.CreatedAt, n.Link
                })
                .ToListAsync();

            return Ok(new {
                total,
                page,
                totalPages = (int)Math.Ceiling((double)total / limit),
                items
            });
        }

        // GET /api/notifications/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = GetUserId()!;
            var count  = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return Ok(new { count });
        }

        // PUT /api/notifications/{id}/read
        [HttpPut("{id:int}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = GetUserId()!;
            var notif  = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notif == null) return NotFound(new { message = "Thông báo không tồn tại" });

            notif.IsRead = true;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã đánh dấu đọc" });
        }

        // PUT /api/notifications/read-all
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = GetUserId()!;
            var unread = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            unread.ForEach(n => n.IsRead = true);
            await _db.SaveChangesAsync();
            return Ok(new { message = $"Đã đánh dấu {unread.Count} thông báo" });
        }

        // DELETE /api/notifications/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId()!;
            var notif  = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notif == null) return NotFound(new { message = "Thông báo không tồn tại" });

            _db.Notifications.Remove(notif);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Thông báo đã được xóa" });
        }
    }
}
