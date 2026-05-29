using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaseCore.APIService.Controllers
{
    [Route("api/admin/users")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly MySqlDbContext _db;

        public UsersController(MySqlDbContext db) => _db = db;

        /// <summary>GET /api/admin/users?search=&amp;role=&amp;isActive=&amp;page=&amp;limit=</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search  = null,
            [FromQuery] int?    role    = null,
            [FromQuery] bool?   isActive = null,
            [FromQuery] int page  = 1,
            [FromQuery] int limit = 20)
        {
            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.Name.Contains(search) ||
                    u.UserName.Contains(search) ||
                    u.Email.Contains(search) ||
                    u.Phone.Contains(search));

            if (role.HasValue)
                query = query.Where(u => u.UserType == role.Value);

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            var total = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.Created)
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(u => new {
                    u.Id,
                    u.Name,
                    u.UserName,
                    u.Email,
                    u.Phone,
                    u.IsActive,
                    u.UserType,
                    createdAt = u.Created
                })
                .ToListAsync();

            return Ok(new { items = users, total, page, totalPages = (int)Math.Ceiling((double)total / limit) });
        }

        /// <summary>GET /api/admin/users/{id} — Chi tiết user</summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var orderCount = await _db.Orders.CountAsync(o => o.UserId == id);
            var shop       = await _db.Shops.FirstOrDefaultAsync(s => s.SellerId == id);

            return Ok(new {
                u.Id, u.Name, u.UserName, u.Email, u.Phone, u.IsActive, u.UserType,
                createdAt = u.Created,
                orderCount,
                shop = shop == null ? null : new { shop.Id, shop.ShopName, shop.Status }
            });
        }

        /// <summary>PUT /api/admin/users/{id}/ban</summary>
        [HttpPut("{id}/ban")]
        public async Task<IActionResult> Ban(string id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound(new { message = "Không tìm thấy người dùng" });
            u.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã khóa tài khoản" });
        }

        /// <summary>PUT /api/admin/users/{id}/unban</summary>
        [HttpPut("{id}/unban")]
        public async Task<IActionResult> Unban(string id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound(new { message = "Không tìm thấy người dùng" });
            u.IsActive = true;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã mở khóa tài khoản" });
        }

        /// <summary>PUT /api/admin/users/{id}/role — Thay đổi role (0=Customer,1=Admin,2=Seller)</summary>
        [HttpPut("{id}/role")]
        public async Task<IActionResult> ChangeRole(string id, [FromBody] ChangeRoleDto dto)
        {
            if (dto.Role < 0 || dto.Role > 2)
                return BadRequest(new { message = "Role không hợp lệ (0=Customer, 1=Admin, 2=Seller)" });

            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            u.UserType = dto.Role;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật quyền", role = dto.Role });
        }

        /// <summary>DELETE /api/admin/users/{id}</summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound(new { message = "Không tìm thấy người dùng" });

            var hasOrders = await _db.Orders.AnyAsync(o => o.UserId == id);
            if (hasOrders)
                return BadRequest(new { message = "Không thể xóa tài khoản đã có lịch sử đơn hàng. Hãy khóa thay thế." });

            _db.Users.Remove(u);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Đã xóa tài khoản" });
        }
    }

    public class ChangeRoleDto
    {
        public int Role { get; set; }
    }
}
