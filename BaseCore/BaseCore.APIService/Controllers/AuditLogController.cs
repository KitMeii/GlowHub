using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    [Route("api/admin/audit-logs")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AuditLogController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public AuditLogController(MySqlDbContext db) => _db = db;

        // GET /api/admin/audit-logs?action=&userId=&from=&to=&page=&limit=
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? action = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? from   = null,
            [FromQuery] string? to     = null,
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 50)
        {
            var fromDate = DateTime.TryParse(from, out var f) ? f.ToUniversalTime() : DateTime.UtcNow.AddDays(-29);
            var toDate   = DateTime.TryParse(to,   out var t) ? t.ToUniversalTime().AddDays(1) : DateTime.UtcNow.AddDays(1);

            var query = _db.AuditLogs
                .Where(a => a.CreatedAt >= fromDate && a.CreatedAt <= toDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(action))
                query = query.Where(a => a.Action.Contains(action));

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(a => a.UserId == userId);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(a => new {
                    a.Id,
                    a.UserId,
                    a.UserName,
                    a.Action,
                    a.EntityType,
                    a.EntityId,
                    a.OldValue,
                    a.NewValue,
                    a.IpAddress,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }
    }
}
