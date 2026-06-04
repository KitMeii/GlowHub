using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/admin/config")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class SystemConfigController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public SystemConfigController(MySqlDbContext db) => _db = db;

        private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        /// <summary>GET /api/admin/config — Lấy tất cả cấu hình hệ thống</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var settings = await _db.SiteSettings
                .OrderBy(s => s.Group)
                .ThenBy(s => s.Key)
                .ToListAsync();

            return Ok(settings);
        }

        /// <summary>GET /api/admin/config/{key} — Lấy 1 cấu hình theo key</summary>
        [HttpGet("{key}")]
        public async Task<IActionResult> GetByKey(string key)
        {
            var s = await _db.SiteSettings.FindAsync(key);
            if (s == null) return NotFound(new { message = "Không tìm thấy cấu hình" });
            return Ok(s);
        }

        /// <summary>PUT /api/admin/config — Upsert nhiều config cùng lúc</summary>
        [HttpPut]
        public async Task<IActionResult> BulkUpsert([FromBody] List<UpsertConfigDto> items)
        {
            if (items == null || !items.Any())
                return BadRequest(new { message = "Không có dữ liệu" });

            var now = DateTime.UtcNow;
            var userId = GetUserId();

            foreach (var item in items)
            {
                var existing = await _db.SiteSettings.FindAsync(item.Key);
                if (existing != null)
                {
                    existing.Value     = item.Value;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = userId;
                    if (!string.IsNullOrWhiteSpace(item.Label)) existing.Label = item.Label;
                }
                else
                {
                    _db.SiteSettings.Add(new SiteSetting
                    {
                        Key       = item.Key,
                        Value     = item.Value,
                        Type      = item.Type ?? "text",
                        Group     = item.Group ?? "general",
                        Label     = item.Label,
                        UpdatedAt = now,
                        UpdatedBy = userId
                    });
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = $"Đã cập nhật {items.Count} cấu hình" });
        }

        /// <summary>PUT /api/admin/config/{key} — Upsert 1 config</summary>
        [HttpPut("{key}")]
        public async Task<IActionResult> Upsert(string key, [FromBody] UpsertConfigDto dto)
        {
            var now = DateTime.UtcNow;
            var userId = GetUserId();
            var existing = await _db.SiteSettings.FindAsync(key);

            if (existing != null)
            {
                existing.Value     = dto.Value;
                existing.UpdatedAt = now;
                existing.UpdatedBy = userId;
            }
            else
            {
                _db.SiteSettings.Add(new SiteSetting
                {
                    Key       = key,
                    Value     = dto.Value,
                    Type      = dto.Type ?? "text",
                    Group     = dto.Group ?? "general",
                    Label     = dto.Label,
                    UpdatedAt = now,
                    UpdatedBy = userId
                });
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật cấu hình", key });
        }

        /// <summary>DELETE /api/admin/config/{key}</summary>
        [HttpDelete("{key}")]
        public async Task<IActionResult> Delete(string key)
        {
            var s = await _db.SiteSettings.FindAsync(key);
            if (s == null) return NotFound(new { message = "Không tìm thấy cấu hình" });
            _db.SiteSettings.Remove(s);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa cấu hình" });
        }
    }

    public class UpsertConfigDto
    {
        public string Key   { get; set; } = "";
        public string? Value { get; set; }
        public string? Type  { get; set; }
        public string? Group { get; set; }
        public string? Label { get; set; }
    }
}
