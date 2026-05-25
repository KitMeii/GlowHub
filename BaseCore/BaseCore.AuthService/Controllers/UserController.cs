using BaseCore.Entities;
using BaseCore.Repository;
using BaseCore.Services.Authen;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BaseCore.AuthService.Controllers
{
    [Route("api/users")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly MySqlDbContext _context;

        // Sửa constructor: inject cả IUserService và MySqlDbContext
        public UserController(IUserService userService, MySqlDbContext context)
        {
            _userService = userService;
            _context = context;
        }

        // GET: api/users?page=1&pageSize=100
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        {
            var query = _context.Users.AsQueryable();

            // Tổng số bản ghi (không lọc IsActive)
            var totalCount = await query.CountAsync();

            // Phân trang
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = users.Select(u => new UserResponse
            {
                Id = u.Id,
                Username = u.UserName,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone,
                Position = u.Position,
                IsActive = u.IsActive,
                UserType = u.UserType,
                Created = u.Created
            });

            return Ok(new
            {
                data = result,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        // GET: api/users/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userService.GetById(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(new UserResponse
            {
                Id = user.Id,
                Username = user.UserName,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Position = user.Position,
                IsActive = user.IsActive,
                UserType = user.UserType,
                Created = user.Created
            });
        }

        // POST: api/users (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Invalid request" });

            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                return BadRequest(new { message = "Username and password are required" });

            try
            {
                var user = new User
                {
                    UserName = request.Username,
                    Name = request.Name ?? request.Username,
                    Email = request.Email,
                    Phone = request.Phone,
                    Position = request.Position,
                    UserType = request.UserType,
                    IsActive = true   // Mặc định active khi tạo mới
                };

                var createdUser = await _userService.Create(user, request.Password);

                return CreatedAtAction(nameof(GetById), new { id = createdUser.Id }, new UserResponse
                {
                    Id = createdUser.Id,
                    Username = createdUser.UserName,
                    Name = createdUser.Name,
                    Email = createdUser.Email,
                    Phone = createdUser.Phone,
                    Position = createdUser.Position,
                    IsActive = createdUser.IsActive,
                    UserType = createdUser.UserType,
                    Created = createdUser.Created
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Failed to create user: " + ex.Message });
            }
        }

        // PUT: api/users/{id} (Admin only) – cập nhật thông tin cơ bản, không đổi mật khẩu
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UserUpdateDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "User not found" });

            // Cập nhật các trường cơ bản
            if (!string.IsNullOrEmpty(dto.Name)) user.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.Email)) user.Email = dto.Email;
            if (!string.IsNullOrEmpty(dto.Phone)) user.Phone = dto.Phone;
            if (dto.UserType.HasValue) user.UserType = dto.UserType.Value;
            if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;

            // Xử lý đổi mật khẩu (nếu có)
            if (!string.IsNullOrEmpty(dto.Password))
            {
                // Dùng BCrypt để mã hóa (đã cài package BCrypt.Net-Next)
                user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);
                // Nếu bạn dùng Salt riêng, cần tạo salt mới và gán vào user.Salt
                // user.Salt = GenerateSalt(); user.Password = HashPassword(dto.Password, user.Salt);
            }

            await _context.SaveChangesAsync();
            return Ok(new UserResponse
            {
                Id = user.Id,
                Username = user.UserName,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                Position = user.Position,
                IsActive = user.IsActive,
                UserType = user.UserType,
                Created = user.Created
            });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null) return NotFound(new { message = "User not found" });

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (user.Id == currentUserId)
                    return BadRequest(new { message = "Cannot delete your own account" });

                // Xóa các bảng liên quan (thứ tự: con trước, cha sau)
                // 1. Xóa OrderDetails (thông qua Orders)
                var orders = await _context.Orders.Where(o => o.UserId == id).ToListAsync();
                foreach (var order in orders)
                {
                    var orderDetails = _context.OrderDetails.Where(od => od.OrderId == order.Id);
                    _context.OrderDetails.RemoveRange(orderDetails);
                }
                _context.Orders.RemoveRange(orders);

                // 2. Xóa CartItems
                var cartItems = _context.CartItems.Where(ci => ci.UserId == id);
                _context.CartItems.RemoveRange(cartItems);

                // 3. Xóa Reviews
                var reviews = _context.Reviews.Where(r => r.UserId == id);
                _context.Reviews.RemoveRange(reviews);


                // 5. Cuối cùng xóa User
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Delete failed: " + ex.Message });
            }
        }
        // ========== DTOs ==========
        public class UserResponse
        {
            public string Id { get; set; }
            public string Username { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Position { get; set; }
            public bool IsActive { get; set; }
            public int UserType { get; set; }
            public DateTime Created { get; set; }

        }

        public class CreateUserRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Position { get; set; }
            public int UserType { get; set; }
        }

        public class UserUpdateDto
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Phone { get; set; }
            public int? UserType { get; set; }
            public bool? IsActive { get; set; }
            public string? Password { get; set; }
        }
    }
}