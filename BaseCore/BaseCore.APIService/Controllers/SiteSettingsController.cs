using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SiteSettingsController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public SiteSettingsController(MySqlDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// GET /api/SiteSettings?group=homepage
        /// Lấy settings theo group (public — dùng cho frontend render)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? group)
        {
            var query = _db.SiteSettings.AsQueryable();
            if (!string.IsNullOrEmpty(group))
                query = query.Where(s => s.Group == group);

            var settings = await query.ToListAsync();

            // Trả về dạng dictionary { key: value } cho frontend dễ dùng
            var dict = settings.ToDictionary(s => s.Key, s => s.Value);
            return Ok(dict);
        }

        /// <summary>
        /// GET /api/SiteSettings/detail — Chi tiết đầy đủ cho admin panel
        /// </summary>
        [HttpGet("detail")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDetail([FromQuery] string? group)
        {
            var query = _db.SiteSettings.AsQueryable();
            if (!string.IsNullOrEmpty(group))
                query = query.Where(s => s.Group == group);
            return Ok(await query.OrderBy(s => s.Group).ThenBy(s => s.Key).ToListAsync());
        }

        /// <summary>
        /// GET /api/SiteSettings/{key} — Lấy 1 setting
        /// </summary>
        [HttpGet("{key}")]
        public async Task<IActionResult> GetByKey(string key)
        {
            var setting = await _db.SiteSettings.FindAsync(key);
            if (setting == null) return NotFound(new { message = $"Key '{key}' not found" });
            return Ok(new { key = setting.Key, value = setting.Value });
        }

        /// <summary>
        /// PUT /api/SiteSettings/{key} — Cập nhật 1 setting (admin only)
        /// Body: { "value": "..." }
        /// </summary>
        [HttpPut("{key}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string key, [FromBody] SettingUpdateDto dto)
        {
            var setting = await _db.SiteSettings.FindAsync(key);
            if (setting == null) return NotFound(new { message = $"Key '{key}' not found" });

            setting.Value = dto.Value;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            await _db.SaveChangesAsync();
            return Ok(setting);
        }

        /// <summary>
        /// PUT /api/SiteSettings/bulk — Cập nhật nhiều settings cùng lúc (admin only)
        /// Body: { "topbar_text": "...", "hero_title": "..." }
        /// </summary>
        [HttpPut("bulk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BulkUpdate([FromBody] Dictionary<string, string> updates)
        {
            var updatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var now = DateTime.UtcNow;
            var updated = new List<string>();

            foreach (var kv in updates)
            {
                var setting = await _db.SiteSettings.FindAsync(kv.Key);
                if (setting != null)
                {
                    setting.Value = kv.Value;
                    setting.UpdatedAt = now;
                    setting.UpdatedBy = updatedBy;
                    updated.Add(kv.Key);
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { updated = updated.Count, keys = updated });
        }
    }

    public class SettingUpdateDto
    {
        public string? Value { get; set; }
    }
}