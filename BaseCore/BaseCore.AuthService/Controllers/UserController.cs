using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BaseCore.Entities;
using BaseCore.Services.Authen;
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
        public UserController(IUserService userService) => _userService = userService;

        private string? GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.Identity?.Name;

        // ★ MỚI: User tự xem profile — GET /api/users/profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userService.GetById(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy user" });

            return Ok(ToResponse(user));
        }

        // ★ MỚI: User tự cập nhật profile — PUT /api/users/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userService.GetById(userId);
            if (user == null) return NotFound(new { message = "Không tìm thấy user" });

            // Chỉ update các field user được phép — không cho đổi UserType, IsActive
            if (!string.IsNullOrWhiteSpace(request.Name)) user.Name = request.Name;
            if (!string.IsNullOrWhiteSpace(request.Email)) user.Email = request.Email;
            if (!string.IsNullOrWhiteSpace(request.Phone)) user.Phone = request.Phone;
            if (!string.IsNullOrWhiteSpace(request.Position)) user.Position = request.Position;

            await _userService.Update(user, null); // null = không đổi password

            return Ok(new { message = "Cập nhật thành công!", user = ToResponse(user) });
        }

        // Admin: lấy tất cả user — GET /api/users
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(
            [FromQuery] string keyword = "",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var (users, totalCount) = await _userService.Search(keyword, page, pageSize);
            return Ok(new
            {
                data = users.Select(ToResponse),
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        // GET /api/users/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userService.GetById(id);
            if (user == null) return NotFound(new { message = "User not found" });
            return Ok(ToResponse(user));
        }

        // Admin: tạo user — POST /api/users
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            if (string.IsNullOrEmpty(request?.Username) || string.IsNullOrEmpty(request.Password))
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
                    UserType = request.UserType
                };
                var created = await _userService.Create(user, request.Password);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, ToResponse(created));
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi tạo user: " + ex.Message });
            }
        }

        // Admin: cập nhật user bất kỳ — PUT /api/users/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request)
        {
            var user = await _userService.GetById(id);
            if (user == null) return NotFound(new { message = "User not found" });

            user.Name = request.Name ?? user.Name;
            user.Email = request.Email ?? user.Email;
            user.Phone = request.Phone ?? user.Phone;
            user.Position = request.Position ?? user.Position;
            user.UserType = request.UserType ?? user.UserType;
            user.IsActive = request.IsActive ?? user.IsActive;

            await _userService.Update(user, request.Password);
            return Ok(ToResponse(user));
        }

        // Admin: xóa user — DELETE /api/users/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userService.GetById(id);
            if (user == null) return NotFound(new { message = "User not found" });
            await _userService.Delete(id);
            return NoContent();
        }

        // Helper: chuyển User → response object
        private static UserResponse ToResponse(User u) => new UserResponse
        {
            Id = u.Id,
            Username = u.UserName,
            Name = u.Name ?? "",
            Email = u.Email ?? "",
            Phone = u.Phone ?? "",
            Position = u.Position ?? "",
            IsActive = u.IsActive,
            UserType = u.UserType,
            Created = u.Created
        };
    }

    // ★ MỚI: DTO cho user tự update (chỉ các field có trong User entity)
    public class UpdateProfileRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Position { get; set; }
    }

    public class UserResponse
    {
        public string Id { get; set; } = "";
        public string Username { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Position { get; set; } = "";
        public bool IsActive { get; set; }
        public int UserType { get; set; }
        public DateTime Created { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Position { get; set; }
        public int UserType { get; set; }
    }

    public class UpdateUserRequest
    {
        public string? Password { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Position { get; set; }
        public int? UserType { get; set; }
        public bool? IsActive { get; set; }
    }
}