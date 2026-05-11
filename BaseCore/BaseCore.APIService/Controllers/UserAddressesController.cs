using BaseCore.Entities;
using BaseCore.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BaseCore.APIService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserAddressesController : ControllerBase
    {
        private readonly MySqlDbContext _ctx;

        public UserAddressesController(MySqlDbContext ctx)
        {
            _ctx = ctx;
        }

        // GET /api/UserAddresses — lấy tất cả địa chỉ của user hiện tại
        [HttpGet]
        public async Task<IActionResult> GetMyAddresses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? User.FindFirstValue("id");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var addresses = await _ctx.UserAddresses
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .Select(a => new {
                    a.Id,
                    a.ReceiverName,
                    a.Phone,
                    a.Province,
                    a.ProvinceCode,
                    a.District,
                    a.DistrictCode,
                    a.Ward,
                    a.WardCode,
                    a.Detail,
                    a.Latitude,
                    a.Longitude,
                    a.MapAddress,
                    a.IsDefault,
                    a.CreatedAt,
                    FullAddress = a.Detail
                        + (string.IsNullOrEmpty(a.Ward) ? "" : ", " + a.Ward)
                        + ", " + a.District
                        + ", " + a.Province
                })
                .ToListAsync();

            return Ok(addresses);
        }

        // POST /api/UserAddresses — thêm địa chỉ mới
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAddressDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? User.FindFirstValue("id");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Nếu set làm mặc định → bỏ mặc định tất cả cái cũ
            if (dto.IsDefault)
            {
                var existing = await _ctx.UserAddresses
                    .Where(a => a.UserId == userId && a.IsDefault)
                    .ToListAsync();
                existing.ForEach(a => a.IsDefault = false);
            }

            // Nếu là địa chỉ đầu tiên → tự động set mặc định
            var count = await _ctx.UserAddresses.CountAsync(a => a.UserId == userId);
            if (count == 0) dto.IsDefault = true;

            var address = new UserAddress
            {
                UserId = userId,
                ReceiverName = dto.ReceiverName,
                Phone = dto.Phone,
                Province = dto.Province,
                ProvinceCode = dto.ProvinceCode ?? "",
                District = dto.District,
                DistrictCode = dto.DistrictCode ?? "",
                Ward = dto.Ward ?? "",
                WardCode = dto.WardCode ?? "",
                Detail = dto.Detail,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                MapAddress = dto.MapAddress,
                IsDefault = dto.IsDefault,
                CreatedAt = DateTime.UtcNow
            };

            _ctx.UserAddresses.Add(address);
            await _ctx.SaveChangesAsync();

            return Ok(new { address.Id, message = "Đã thêm địa chỉ thành công" });
        }

        // PUT /api/UserAddresses/{id}/set-default — đặt làm mặc định
        [HttpPut("{id}/set-default")]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? User.FindFirstValue("id");

            // Bỏ mặc định tất cả
            var all = await _ctx.UserAddresses
                .Where(a => a.UserId == userId)
                .ToListAsync();
            all.ForEach(a => a.IsDefault = false);

            // Set mặc định cho địa chỉ được chọn
            var target = all.FirstOrDefault(a => a.Id == id);
            if (target == null) return NotFound();
            target.IsDefault = true;
            target.UpdatedAt = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();
            return Ok(new { message = "Đã đặt địa chỉ mặc định" });
        }

        // PUT /api/UserAddresses/{id} — cập nhật địa chỉ
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CreateAddressDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? User.FindFirstValue("id");

            var address = await _ctx.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) return NotFound();

            if (dto.IsDefault)
            {
                var existing = await _ctx.UserAddresses
                    .Where(a => a.UserId == userId && a.IsDefault && a.Id != id)
                    .ToListAsync();
                existing.ForEach(a => a.IsDefault = false);
            }

            address.ReceiverName = dto.ReceiverName;
            address.Phone = dto.Phone;
            address.Province = dto.Province;
            address.ProvinceCode = dto.ProvinceCode ?? "";
            address.District = dto.District;
            address.DistrictCode = dto.DistrictCode ?? "";
            address.Ward = dto.Ward ?? "";
            address.WardCode = dto.WardCode ?? "";
            address.Detail = dto.Detail;
            address.Latitude = dto.Latitude;
            address.Longitude = dto.Longitude;
            address.MapAddress = dto.MapAddress;
            address.IsDefault = dto.IsDefault;
            address.UpdatedAt = DateTime.UtcNow;

            await _ctx.SaveChangesAsync();
            return Ok(new { message = "Đã cập nhật địa chỉ" });
        }

        // DELETE /api/UserAddresses/{id} — xóa địa chỉ
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub")
                      ?? User.FindFirstValue("id");

            var address = await _ctx.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
            if (address == null) return NotFound();

            _ctx.UserAddresses.Remove(address);
            await _ctx.SaveChangesAsync();

            // Nếu xóa mặc định → set mặc định cho địa chỉ đầu tiên còn lại
            if (address.IsDefault)
            {
                var first = await _ctx.UserAddresses
                    .Where(a => a.UserId == userId)
                    .OrderBy(a => a.CreatedAt)
                    .FirstOrDefaultAsync();
                if (first != null)
                {
                    first.IsDefault = true;
                    await _ctx.SaveChangesAsync();
                }
            }

            return Ok(new { message = "Đã xóa địa chỉ" });
        }
    }

    // DTO
    public class CreateAddressDto
    {
        public string ReceiverName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Province { get; set; } = "";
        public string? ProvinceCode { get; set; }
        public string District { get; set; } = "";
        public string? DistrictCode { get; set; }
        public string? Ward { get; set; }
        public string? WardCode { get; set; }
        public string Detail { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? MapAddress { get; set; }
        public bool IsDefault { get; set; } = false;
    }
}