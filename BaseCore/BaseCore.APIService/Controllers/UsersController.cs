using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [Route("api/admin/users")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly MySqlDbContext _db;
        private readonly AuditLogService _audit;

        public UsersController(MySqlDbContext db, AuditLogService audit)
        {
            _db    = db;
            _audit = audit;
        }

        private string? GetUserId()   => User.FindFirstValue(ClaimTypes.NameIdentifier);
        private string? GetUserName() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");

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
            if (u.Id == GetUserId())
                return BadRequest(new { message = "Không thể tự khóa chính mình" });
            if (u.UserType == 1 && await IsLastActiveAdmin(u.Id))
                return BadRequest(new { message = "Không thể khóa Admin cuối cùng còn hoạt động" });

            u.IsActive = false;
            u.TokenVersion++;   // force-logout user trên mọi thiết bị
            await _db.SaveChangesAsync();
            await _audit.Log(GetUserId(), GetUserName(), "USER_BAN", "User", id,
                new { isActive = true }, new { isActive = false });
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
            await _audit.Log(GetUserId(), GetUserName(), "USER_UNBAN", "User", id,
                new { isActive = false }, new { isActive = true });
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

            var oldRole = u.UserType;
            if (oldRole == dto.Role)
                return Ok(new { message = "Vai trò không đổi", role = dto.Role });

            // ── Safeguard: tránh tự khóa quyền admin của chính mình + đảm bảo còn Admin ──
            if (u.Id == GetUserId() && dto.Role != 1)
                return BadRequest(new { message = "Không thể tự gỡ quyền Admin của chính mình" });
            if (oldRole == 1 && dto.Role != 1 && await IsLastActiveAdmin(u.Id))
                return BadRequest(new { message = "Không thể demote Admin cuối cùng còn hoạt động" });

            // ── Ràng buộc Shop khi đổi từ/sang Seller ──
            // Promote → Seller: yêu cầu user đã có Shop (đăng ký qua register-shop.html trước)
            //                   để không tạo "Seller ma" không có cửa hàng.
            if (dto.Role == 2)
            {
                var hasShop = await _db.Shops.AnyAsync(s => s.SellerId == u.Id);
                if (!hasShop)
                    return BadRequest(new {
                        message = "Người dùng chưa có cửa hàng — yêu cầu họ đăng ký Shop trước khi cấp quyền Seller."
                    });
            }
            // Demote Seller → Customer/Admin: chặn nếu còn Shop đang Active (mồ côi sản phẩm/đơn).
            if (oldRole == 2 && dto.Role != 2)
            {
                var hasActiveShop = await _db.Shops.AnyAsync(s => s.SellerId == u.Id && s.Status == ShopStatus.Active);
                if (hasActiveShop)
                    return BadRequest(new {
                        message = "Người dùng vẫn còn cửa hàng đang hoạt động — hãy khóa Shop ở tab Cửa Hàng trước."
                    });
            }

            u.UserType = dto.Role;
            u.TokenVersion++;   // JWT hiện tại của user mất hiệu lực → lần request kế tiếp bị 401 → redirect login → tự load lại đúng giao diện theo role mới
            await _db.SaveChangesAsync();
            await _audit.Log(GetUserId(), GetUserName(), "USER_CHANGE_ROLE", "User", id,
                new { userType = oldRole }, new { userType = dto.Role });
            return Ok(new { message = "Đã cập nhật quyền — phiên cũ của người dùng đã bị thu hồi", role = dto.Role });
        }

        // Đếm Admin còn IsActive=true ngoài chính user đang được tác động.
        private async Task<bool> IsLastActiveAdmin(string excludeUserId)
        {
            var hasOtherAdmin = await _db.Users
                .AnyAsync(x => x.UserType == 1 && x.IsActive && x.Id != excludeUserId);
            return !hasOtherAdmin;
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
