using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BannersController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public BannersController(MySqlDbContext db)
        {
            _db = db;
        }

        /// <summary>GET /api/Banners — Lấy tất cả banner đang active (public)</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var banners = await _db.Banners
                .Where(b => b.IsActive)
                .OrderBy(b => b.SortOrder)
                .ToListAsync();
            return Ok(banners);
        }

        /// <summary>GET /api/Banners/all — Lấy tất cả kể cả inactive (admin only)</summary>
        [HttpGet("all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllAdmin()
        {
            var banners = await _db.Banners
                .OrderBy(b => b.SortOrder)
                .ToListAsync();
            return Ok(banners);
        }

        /// <summary>GET /api/Banners/{id}</summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(int id)
        {
            var banner = await _db.Banners.FindAsync(id);
            if (banner == null) return NotFound(new { message = "Banner not found" });
            return Ok(banner);
        }

        /// <summary>POST /api/Banners — Tạo banner mới (admin only)</summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] BannerDto dto)
        {
            var banner = new Banner
            {
                Title = dto.Title,
                Subtitle = dto.Subtitle,
                ImageUrl = dto.ImageUrl ?? "",
                LinkUrl = dto.LinkUrl,
                ButtonText = dto.ButtonText,
                BgColor = dto.BgColor,
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            };

            _db.Banners.Add(banner);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = banner.Id }, banner);
        }

        /// <summary>PUT /api/Banners/{id} — Cập nhật banner (admin only)</summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] BannerDto dto)
        {
            var banner = await _db.Banners.FindAsync(id);
            if (banner == null) return NotFound(new { message = "Banner not found" });

            banner.Title = dto.Title ?? banner.Title;
            banner.Subtitle = dto.Subtitle ?? banner.Subtitle;
            banner.ImageUrl = dto.ImageUrl ?? banner.ImageUrl;
            banner.LinkUrl = dto.LinkUrl ?? banner.LinkUrl;
            banner.ButtonText = dto.ButtonText ?? banner.ButtonText;
            banner.BgColor = dto.BgColor ?? banner.BgColor;
            banner.SortOrder = dto.SortOrder;
            banner.IsActive = dto.IsActive;
            banner.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(banner);
        }

        /// <summary>DELETE /api/Banners/{id} (admin only)</summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var banner = await _db.Banners.FindAsync(id);
            if (banner == null) return NotFound(new { message = "Banner not found" });

            _db.Banners.Remove(banner);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa banner" });
        }

        /// <summary>PUT /api/Banners/reorder — Cập nhật thứ tự (admin only)</summary>
        [HttpPut("reorder")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reorder([FromBody] List<BannerOrderDto> items)
        {
            foreach (var item in items)
            {
                var banner = await _db.Banners.FindAsync(item.Id);
                if (banner != null)
                {
                    banner.SortOrder = item.SortOrder;
                }
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật thứ tự" });
        }
    }

    public class BannerDto
    {
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public string? ImageUrl { get; set; }
        public string? LinkUrl { get; set; }
        public string? ButtonText { get; set; }
        public string? BgColor { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class BannerOrderDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }
}